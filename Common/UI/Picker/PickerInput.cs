using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// Reading the mouse for the picker: edge detection, what is under the cursor, and what a click or a
/// release means. Kept in one piece because the frame ordering here is subtle and worth reading
/// together.
/// </summary>
internal static class PickerInput
{
	/// <summary>A keybind released within this many ticks of opening was tapped, and sticks the picker open.</summary>
	private const int StickyGraceTicks = 10;

	/// <summary>How far the cursor must travel before a release counts as a choice rather than a cancel.</summary>
	private const float MovedThreshold = 7f;

	/// <summary>Where the cursor was when the picker or list opened, and whether it has moved since.</summary>
	internal static Vector2 OpenCursor;

	internal static bool CursorMoved;
	private static bool leftHeldLast;
	private static bool rightHeldLast;
	private static bool freshLeftClick;
	private static bool freshRightClick;

	// What the cursor is over, one field per kind of target; -1 or false for none. Written by
	// UpdateHover each tick, read by the clicks, the release and the drawing.
	internal static int HoveredSwatch = -1;
	internal static int HoveredCoating = -1;
	internal static int HoveredArrow = -1;
	internal static int HoveredRow = -1;
	internal static bool HoveredCenter;
	internal static bool HoveredHeader;
	internal static int HoveredCell = -1;
	internal static int HoveredAction = -1;
	internal static bool HoveredPlus;
	internal static bool HoveredDone;

	/// <summary>True while the cursor is anywhere over an open list's panel, rows or not.</summary>
	internal static bool HoveredPanel;

	/// <summary>
	/// The saved palette whose bin has been clicked once, or -1. Deleting is permanent and saved at once,
	/// so it takes a second click on the same bin; moving off it forgets the first.
	/// </summary>
	internal static int ArmedDelete = -1;

	/// <summary>A button already down when the picker opens is not a click on it.</summary>
	internal static void PrimeButtons()
	{
		leftHeldLast = Main.mouseLeft;
		rightHeldLast = Main.mouseRight;
		freshLeftClick = false;
		freshRightClick = false;
	}

	/// <summary>Forgets the cursor and the buttons, for when the picker is reset.</summary>
	internal static void ResetButtons()
	{
		CursorMoved = false;
		OpenCursor = Vector2.Zero;
		leftHeldLast = false;
		rightHeldLast = false;
		freshLeftClick = false;
		freshRightClick = false;
	}

	/// <summary>Forgets what the cursor was over, in every view.</summary>
	internal static void ClearHover()
	{
		HoveredCell = -1;
		HoveredAction = -1;
		HoveredPlus = false;
		HoveredDone = false;
		HoveredSwatch = -1;
		HoveredCoating = -1;
		HoveredArrow = -1;
		HoveredRow = -1;
		HoveredCenter = false;
		HoveredHeader = false;
		HoveredPanel = false;
		ArmedDelete = -1;
	}

	/// <summary>
	/// Edge detection against our own previous sample, because the vanilla idiom cannot work here:
	/// UpdateUI runs before PlayerInput.UpdateInput assigns Main.mouseLeft, while Main.mouseLeftRelease
	/// was set to !Main.mouseLeft at the end of the last draw. The pair is inverted at this point in the
	/// tick, so "mouseLeft &amp;&amp; mouseLeftRelease" is never true and buttons read that way are dead.
	/// One frame late, like Terraria's own UIElement layer.
	/// </summary>
	internal static void SampleButtons()
	{
		bool left = Main.mouseLeft;
		bool right = Main.mouseRight;

		freshLeftClick = left && !leftHeldLast;
		freshRightClick = right && !rightHeldLast;

		leftHeldLast = left;
		rightHeldLast = right;
	}

	/// <summary>
	/// A left click is the one button free while the picker is held open on right click, so it drives
	/// every button here.
	/// </summary>
	internal static void HandleClicks(PaintWheelConfig config)
	{
		if (!freshLeftClick)
			return;

		// A click anywhere but the name being typed keeps it, then does what it would have done.
		if (PickerMenu.IsRenaming) {
			bool onRenamed = HoveredRow >= 0 && HoveredRow < PickerMenu.Rows.Count
				&& PickerMenu.Rows[HoveredRow].Preset == PickerMenu.Renaming && HoveredAction < 0;

			if (onRenamed) {
				Consume();
				return;
			}

			PickerMenu.CommitRename(config);
		}

		// Editing: a click puts a colour in the palette or takes it out, or finishes.
		if (PaintPicker.Overlay == PickerOverlay.Grid) {
			if (HoveredDone) {
				PaintPicker.FinishEditing(config);
				PaintPicker.Play(SoundID.MenuClose, config);
				Consume();
				return;
			}

			if (HoveredCell >= 0 && HoveredCell < PaletteEditor.GridPaints.Count) {
				PaletteEditor.ToggleMember(PaletteEditor.GridPaints[HoveredCell], config);
				PaintPicker.Play(SoundID.MenuTick, config);
			}

			Consume();
			return;
		}

		if (PaintPicker.Overlay == PickerOverlay.Palettes && HoveredPlus) {
			PaintPicker.Play(PaintPicker.CreateAndEdit(config) ? SoundID.Grab : SoundID.MenuClose, config);
			Consume();
			return;
		}

		if (PaintPicker.Overlay == PickerOverlay.Palettes && HoveredRow >= 0 && HoveredAction >= 0) {
			MenuRow entry = PickerMenu.Rows[HoveredRow];

			if (HoveredAction == PickerMenu.ActionDelete && ArmedDelete != entry.Preset) {
				ArmedDelete = entry.Preset;
				PaintPicker.Play(SoundID.MenuTick, config);
				Consume();
				return;
			}

			ArmedDelete = -1;
			bool done = HoveredAction switch {
				PickerMenu.ActionEdit => PaintPicker.StartEditing(entry.Preset, config),
				PickerMenu.ActionRename => PaintPicker.StartRenaming(entry.Preset, config),
				_ => PaletteEditor.DeletePreset(entry.Preset, config),
			};

			PaintPicker.Play(done ? SoundID.Grab : SoundID.MenuClose, config);
			Consume();
			return;
		}

		if (PaintPicker.Overlay != PickerOverlay.None) {
			if (HoveredRow >= 0) {
				RowResult result = PaintPicker.ActivateRow(HoveredRow, config);
				PaintPicker.Play(result == RowResult.Failed ? SoundID.MenuClose : SoundID.Grab, config);

				if (result == RowResult.CloseAfter && !PaintPicker.Sticky)
					PaintPicker.CloseSilently();
			}
			else if (!HoveredPanel) {
				// Clicking off the list backs out: to the swatches if there are any, otherwise away. A
				// click on the panel between rows is a near miss, not a request to leave.
				PaintPicker.Play(SoundID.MenuClose, config);

				if (PaintPicker.Overlay == PickerOverlay.Scrape)
					PaintPicker.CloseSilently();
				else
					PaintPicker.Overlay = PickerOverlay.None;
			}

			Consume();
			return;
		}

		if (HoveredArrow >= 0) {
			PaintPicker.ChangePage(HoveredArrow == 0 ? -1 : 1, config);
			Consume();
			return;
		}

		// The scrape button takes a click as well as a release, like every other button here.
		if (PickerContent.IsScrapeButton(HoveredCoating)) {
			if (PaintPicker.EnterScrapeFromRow(config))
				PaintPicker.Play(SoundID.Grab, config);
			else
				PaintPicker.Play(SoundID.MenuClose, config);

			Consume();
			return;
		}

		if ((HoveredCenter || HoveredHeader) && PaintPicker.MenuAvailable) {
			PaintPicker.OpenOverlay(PickerOverlay.Palettes, config);
			Consume();
			return;
		}

		// The bottom row is settings, not the choice the picker exists to make: none of it closes
		// anything, so it answers a left click while right is still held, exactly like the scraper
		// button beside it.
		if (HoveredCoating >= 0 && HoveredCoating < PickerContent.CoatingRow.Count) {
			PaintSelection.SelectCoating(PickerContent.CoatingRow[HoveredCoating]);
			PaintPicker.Play(SoundID.Grab, config);
			Consume();
			return;
		}

		// Switches, not colours, so the picker stays up and they can be flipped straight back.
		if (PickerContent.IsNoPaintButton(HoveredCoating)) {
			PaintSelection.ToggleNoPaint();
			PaintPicker.Play(SoundID.Grab, config);
			Consume();
			return;
		}

		if (PickerContent.IsPaintBothButton(HoveredCoating)) {
			PaintSelection.TogglePaintBoth();
			PaintPicker.Play(SoundID.Grab, config);
			Consume();
			return;
		}

		// A colour is what a held picker commits on release, so a stray left click must not take it.
		if (!PaintPicker.Sticky)
			return;

		if (HoveredSwatch >= 0 && HoveredSwatch < PickerContent.Swatches.Count) {
			// A colour is the one choice that finishes the job, so it is the one that closes.
			PaintSelection.SelectPaint(PickerContent.Swatches[HoveredSwatch]);
			PaintPicker.Play(SoundID.Grab, config);
			Consume();
			PaintPicker.CloseSilently();
			return;
		}

		if (CursorMoved) {
			PaintPicker.Play(SoundID.MenuClose, config);
			Consume();
			PaintPicker.CloseSilently();
		}
	}

	/// <summary>Marks the click as ours, the way vanilla UI does, so nothing else acts on it too.</summary>
	private static void Consume()
	{
		freshLeftClick = false;

		// Also cleared the way vanilla UI clears it, so nothing later in the frame acts on the same
		// press. It is recomputed from the button state at the end of the draw either way.
		Main.mouseLeftRelease = false;
	}

	/// <summary>Right click and the keybind commit on release; a tapped keybind sticks the picker open.</summary>
	internal static bool ShouldCommit(PaintWheelConfig config)
	{
		if (!PaintPicker.ViaKeybind)
			return !Main.mouseRight;

		var key = KeybindSystem.OpenWheelKey;
		if (key is null)
			return true;

		if (key.Current)
			return false;

		// Tapped rather than held: stick open instead, and let clicking do the choosing from here.
		if (PaintPicker.FramesOpen <= StickyGraceTicks) {
			PaintPicker.Sticky = true;
			return false;
		}

		return true;
	}

	/// <summary>
	/// What puts a stuck-open picker away: pressing the trigger again, or the keybind that opened it.
	/// A left click never dismisses, because left clicks are how everything in this mode is chosen.
	/// </summary>
	internal static bool ShouldDismiss()
	{
		if (PaintPicker.ViaKeybind && KeybindSystem.OpenWheelKey is { JustPressed: true })
			return true;

		return !PaintPicker.ViaKeybind && freshRightClick;
	}

	/// <summary>
	/// Scroll walks pages, or changes palette when there is only one page so a scroll is never answered
	/// with nothing. Shift always changes palette.
	/// </summary>
	internal static void HandleScroll(PaintWheelConfig config)
	{
		int delta = PlayerInput.ScrollWheelDeltaForUI;

		// Claimed so nothing else in the UI pass acts on the same notch. The hotbar is handled in
		// PaintWheelUISystem.PreUpdatePlayers, because UpdateInput overwrites this later in the tick.
		PlayerInput.ScrollWheelDelta = 0;
		PlayerInput.ScrollWheelDeltaForUI = 0;

		if (delta == 0)
			return;

		int direction = delta > 0 ? -1 : 1;
		bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);

		// Nothing to scroll through in the scrape list, and changing palette under the editor would
		// leave it editing one you can no longer see. The delta is still swallowed above so the hotbar
		// does not move underneath either.
		if (PaintPicker.Overlay is PickerOverlay.Scrape or PickerOverlay.Grid)
			return;

		// A list longer than fits scrolls instead, so every palette in it can be reached.
		if (PaintPicker.Overlay == PickerOverlay.Palettes && PickerMenu.Rows.Count > PickerMenu.ShownRows) {
			if (PickerMenu.Scroll(direction))
				PaintPicker.Play(SoundID.MenuTick, config);

			return;
		}

		if (PaintPicker.Overlay == PickerOverlay.Palettes || shift || PickerContent.PageCount(config) <= 1)
			PaintPicker.CyclePalette(direction, config);
		else
			PaintPicker.ChangePage(direction, config);
	}

	internal static void UpdateHover(PaintWheelConfig config)
	{
		Vector2 cursor = Main.MouseScreen;

		if (!CursorMoved && Vector2.DistanceSquared(cursor, OpenCursor) > MovedThreshold * MovedThreshold)
			CursorMoved = true;

		WheelLayout.Settings settings = PaintPicker.BuildSettings(config, WheelMath.EaseOut(PaintPicker.Anim));
		WheelLayout.Geometry geometry = WheelLayout.Compute(settings, PaintPicker.Anchor);

		// A gamepad points with the right stick from the picker's middle, the way a radial menu is used,
		// rather than dragging the cursor out to a swatch.
		if (PlayerInput.UsingGamepad && PlayerInput.GamepadThumbstickRight.LengthSquared() > StickDeadZone * StickDeadZone) {
			cursor = StickCursor(settings, geometry, PlayerInput.GamepadThumbstickRight);
			CursorMoved = true;
		}

		// The centre and header are live immediately; swatches wait for the cursor to move, so a picker
		// opening under the cursor cannot commit what sits there.
		bool overlaid = PaintPicker.Overlay != PickerOverlay.None;

		HoveredCenter = settings.Style == WheelLayoutStyle.Wheel && !overlaid && PaintPicker.MenuAvailable
			&& Vector2.Distance(cursor, PaintPicker.Anchor) <= CenterHitRadius(config);

		HoveredHeader = !overlaid && PaintPicker.MenuAvailable && settings.ShowHeader
			&& geometry.HeaderBox.Contains((int)cursor.X, (int)cursor.Y);

		if (PaintPicker.Overlay == PickerOverlay.Grid) {
			Vector2 gridAt = PaletteEditor.GridCenter(geometry);

			HoveredDone = CursorMoved
				&& WheelLayout.GridTitleAction(gridAt, PaletteEditor.GridShape).Contains((int)cursor.X, (int)cursor.Y);

			int cell = CursorMoved && !HoveredDone
				? WheelLayout.HitTestGrid(gridAt, PaletteEditor.GridShape, cursor)
				: -1;

			// Ticks on reaching a cell, not on leaving one, so crossing the grid is one tick per colour.
			if (cell >= 0 && cell != HoveredCell)
				PaintPicker.Play(SoundID.MenuTick, config);

			HoveredCell = cell;
			HoveredRow = -1;
			HoveredSwatch = -1;
			HoveredCoating = -1;
			HoveredArrow = -1;
			return;
		}

		if (overlaid) {
			Vector2 menuAt = PickerMenu.MenuCenter(geometry);

			int row = CursorMoved
				? PickerMenu.HitTestRow(menuAt, cursor)
				: -1;

			if (row >= 0 && row != HoveredRow)
				PaintPicker.Play(SoundID.MenuTick, config);

			HoveredRow = row;
			HoveredAction = PickerMenu.HitTestRowActions(menuAt, cursor);
			HoveredPanel = PickerMenu.Rows.Count > 0
				&& WheelLayout.MenuBounds(menuAt, PickerMenu.Shape).Contains((int)cursor.X, (int)cursor.Y);

			if (ArmedDelete >= 0 && !(HoveredAction == PickerMenu.ActionDelete && PickerMenu.Rows[HoveredRow].Preset == ArmedDelete))
				ArmedDelete = -1;

			HoveredPlus = PaintPicker.Overlay == PickerOverlay.Palettes && CursorMoved
				&& WheelLayout.MenuTitleAction(menuAt, PickerMenu.Shape).Contains((int)cursor.X, (int)cursor.Y);
			HoveredSwatch = -1;
			HoveredCoating = -1;
			HoveredArrow = -1;
			return;
		}

		HoveredRow = -1;
		HoveredPanel = false;
		HoveredArrow = HitTestArrows(config, geometry, cursor);

		// An arrow no longer hides the swatch behind it. Sitting beside the swatches means a flick that
		// overshoots can land on one, and a release there has to pick the colour that was aimed at -
		// the arrow answers to a click, never to a release.
		bool blocked = !CursorMoved || HoveredCenter || HoveredHeader;

		int coating = blocked ? -1 : WheelLayout.HitTestCoating(settings, geometry, cursor);
		int swatch = blocked || coating >= 0 ? -1 : WheelLayout.HitTestSwatch(settings, geometry, cursor);

		if ((swatch >= 0 && swatch != HoveredSwatch) || (coating >= 0 && coating != HoveredCoating))
			PaintPicker.Play(SoundID.MenuTick, config);

		HoveredSwatch = swatch;
		HoveredCoating = coating;
	}

	/// <summary>How far the right stick must lean before it points at anything.</summary>
	private const float StickDeadZone = 0.35f;

	/// <summary>
	/// Where the right stick is pointing, as a cursor: from the middle of whatever is on show, out to its
	/// edge. The stick's Y already runs down the screen (PlayerInput flips it).
	/// </summary>
	private static Vector2 StickCursor(in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, Vector2 stick)
	{
		Rectangle area;

		switch (PaintPicker.Overlay) {
			case PickerOverlay.Grid:
				area = WheelLayout.GridBounds(PaletteEditor.GridCenter(geometry), PaletteEditor.GridShape);
				area.Inflate(-(int)WheelLayout.GridPadding - 1, -(int)WheelLayout.GridPadding - 1);
				break;

			// A radial menu: which way the stick leans is the swatch, whatever the distance.
			case PickerOverlay.None when settings.Style == WheelLayoutStyle.Wheel:
				return PaintPicker.Anchor + stick * geometry.Radius;

			// The whole picker - header, arrows and bottom row as well as the swatches.
			case PickerOverlay.None:
				area = geometry.Bounds;
				area.Inflate(-2, -2);
				break;

			default:
				area = WheelLayout.MenuBounds(PickerMenu.MenuCenter(geometry), PickerMenu.Shape);
				area.Inflate(-(int)WheelLayout.MenuPadding - 1, -(int)WheelLayout.MenuPadding - 1);
				break;
		}

		// The dead zone's worth of lean is taken off first, or everything within it of the middle - in
		// the grid, whole columns of swatches - could never be pointed at. Then stretched from the
		// stick's circle to the rectangle, so leaning into a corner reaches it: the list's "+" and the
		// editor's Done both sit in one.
		float length = stick.Length();
		float reach = Math.Clamp((length - StickDeadZone) / (1f - StickDeadZone), 0f, 1f);
		float lean = Math.Max(Math.Abs(stick.X), Math.Abs(stick.Y));
		Vector2 square = lean > 0f ? stick * (reach / lean) : Vector2.Zero;

		return area.Center.ToVector2() + square * new Vector2(area.Width * 0.5f, area.Height * 0.5f);
	}

	/// <summary>How close to the anchor counts as the centre button.</summary>
	private static float CenterHitRadius(PaintWheelConfig config)
		=> MathF.Max(20f, MathF.Min(config.Appearance.DeadZoneRadius, config.Appearance.SwatchSize * 0.5f));

	private static int HitTestArrows(PaintWheelConfig config, in WheelLayout.Geometry geometry, Vector2 cursor)
	{
		// Waits for the cursor to move, like the swatches: a click where the picker opened is not aimed.
		if (!CursorMoved || PaintPicker.Overlay != PickerOverlay.None || PickerContent.PageCount(config) <= 1)
			return -1;

		var point = new Point((int)cursor.X, (int)cursor.Y);
		if (geometry.LeftArrow.Contains(point))
			return 0;

		return geometry.RightArrow.Contains(point) ? 1 : -1;
	}

	/// <summary>Same rule as the wheel: nothing is chosen until the cursor moves onto a row.</summary>
	internal static void ResetMenuCursor()
	{
		HoveredRow = -1;
		OpenCursor = Main.MouseScreen;
		CursorMoved = false;
	}
}
