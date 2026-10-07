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
	internal static bool HoveredCenter;
	internal static bool HoveredHeader;

	/// <summary>On the scrape wheel: an option, <see cref="WheelLayout.ScrapeBack"/> for the disc in the middle, or -1.</summary>
	internal static int HoveredScrape = -1;

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
		HoveredSwatch = -1;
		HoveredCoating = -1;
		HoveredArrow = -1;
		HoveredCenter = false;
		HoveredHeader = false;
		HoveredScrape = -1;
		PaletteBoard.ClearHover();
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

		// The board: its own buttons, pills and paints. A click off it goes back to the colours.
		if (PaintPicker.Overlay == PickerOverlay.Palettes) {
			if (!PaletteBoard.Click(config)) {
				PaintPicker.CloseBoard();
				PaintPicker.Play(SoundID.MenuClose, config);
			}

			Consume();
			return;
		}

		// The scrape wheel. An option is the choice that finishes the job here, as a colour is on the
		// colour wheel, so it closes the picker even when it was left up - and, as there, a click in any
		// direction lands on the option that way. The middle goes back to the colours and keeps the
		// picker up to choose one.
		if (PaintPicker.Overlay == PickerOverlay.Scrape) {
			if (HoveredScrape == WheelLayout.ScrapeBack) {
				PaintPicker.Play(PaintPicker.BackToColours(config) ? SoundID.Grab : SoundID.MenuClose, config);
			}
			else if (HoveredScrape >= 0) {
				bool chosen = PaintPicker.ChooseScrapeTarget(HoveredScrape);
				PaintPicker.Play(chosen ? SoundID.Grab : SoundID.MenuClose, config);

				if (chosen)
					PaintPicker.CloseSilently();
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
			PaintPicker.OpenBoard(config);
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

		// Nothing to scroll through on the scrape wheel. The delta is still swallowed above so the hotbar
		// does not move underneath either.
		if (PaintPicker.Overlay == PickerOverlay.Scrape)
			return;

		// The board scrolls what is under the cursor: its row of palettes, or its paints.
		if (PaintPicker.Overlay == PickerOverlay.Palettes) {
			if (PaletteBoard.Scroll(direction, config))
				PaintPicker.Play(SoundID.MenuTick, config);

			return;
		}

		if (shift || PickerContent.PageCount(config) <= 1)
			PaintPicker.CyclePalette(direction, config);
		else
			PaintPicker.ChangePage(direction, config);
	}

	internal static void UpdateHover(PaintWheelConfig config)
	{
		Vector2 cursor = Main.MouseScreen;
		int wasScrape = HoveredScrape;
		HoveredScrape = -1;

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

		if (PaintPicker.Overlay == PickerOverlay.Palettes) {
			PaletteBoard.Place(geometry);
			PaletteBoard.UpdateHover(cursor, config);
			HoveredSwatch = -1;
			HoveredCoating = -1;
			HoveredArrow = -1;
			return;
		}

		if (PaintPicker.Overlay == PickerOverlay.Scrape) {
			int target = ScrapeWheel.HitTest(settings, cursor, CursorMoved);

			// Ticks on reaching an option, as on a swatch.
			if (target >= 0 && target != wasScrape)
				PaintPicker.Play(SoundID.MenuTick, config);

			HoveredScrape = target;
			HoveredSwatch = -1;
			HoveredCoating = -1;
			HoveredArrow = -1;
			return;
		}

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
			// A radial menu: which way the stick leans is the swatch, whatever the distance.
			case PickerOverlay.None when settings.Style == WheelLayoutStyle.Wheel:
				return PaintPicker.Anchor + stick * geometry.Radius;

			// The scrape wheel is one too, whatever shape the picker is.
			case PickerOverlay.Scrape:
				return ScrapeWheel.Center + stick * ScrapeWheel.Shape(settings).Radius;

			// The whole picker - header, arrows and bottom row as well as the swatches.
			case PickerOverlay.None:
				area = geometry.Bounds;
				area.Inflate(-2, -2);
				break;

			// The palette board, edge to edge: its buttons sit right out at its side.
			default:
				area = PaletteBoard.Bounds;
				area.Inflate(-8, -8);
				break;
		}

		// The dead zone's worth of lean is taken off first, or everything within it of the middle - in
		// the grid, whole columns of swatches - could never be pointed at. Then stretched from the
		// stick's circle to the rectangle, so leaning into a corner reaches it: the board's buttons sit
		// out at its edge.
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

	/// <summary>Same rule as the wheel, for a view just switched to: nothing is chosen until the cursor moves.</summary>
	internal static void ResetMenuCursor()
	{
		OpenCursor = Main.MouseScreen;
		CursorMoved = false;
	}
}
