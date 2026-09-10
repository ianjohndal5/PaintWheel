using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PaintWheel.Common;
using PaintWheel.Configs;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;

namespace PaintWheel.UI;

/// <summary>
/// Reading the mouse for the picker: edge detection, what is under the cursor, and what a click or a
/// release means. Split from the state itself because the frame ordering here is subtle and worth
/// reading in one piece.
/// </summary>
public static partial class PaintWheelState
{
	/// <summary>
	/// Edge detection against our own previous sample, because the vanilla idiom cannot work here:
	/// UpdateUI runs before PlayerInput.UpdateInput assigns Main.mouseLeft, while Main.mouseLeftRelease
	/// was set to !Main.mouseLeft at the end of the last draw. The pair is inverted at this point in the
	/// tick, so "mouseLeft &amp;&amp; mouseLeftRelease" is never true and buttons read that way are dead.
	/// One frame late, like Terraria's own UIElement layer.
	/// </summary>
	private static void SampleButtons()
	{
		bool left = Main.mouseLeft;
		bool right = Main.mouseRight;

		freshLeftClick = left && !leftHeldLast;
		freshRightClick = right && !rightHeldLast;

		leftHeldLast = left;
		rightHeldLast = right;
	}

	private static bool ShouldCancel(PaintWheelConfig config)
		=> EnvironmentBlocked(config) || (overlay != Overlay.Scrape && !HasAnythingToShow);

	/// <summary>Swatches, coatings, or a list worth opening. Any of the three is reason to be up.</summary>
	private static bool HasAnythingToShow => swatches.Count > 0 || RowSlots > 0 || MenuAvailable;

	/// <summary>Everything about the world state that means the picker must not be up.</summary>
	private static bool EnvironmentBlocked(PaintWheelConfig config)
	{
		if (Main.gameMenu || Main.mapFullscreen || Main.playerInventory || Main.ingameOptionsWindow)
			return true;

		if (Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput)
			return true;

		if (Main.keyState.IsKeyDown(Keys.Escape))
			return true;

		Player player = Main.LocalPlayer;
		if (player is null || !player.active || player.dead)
			return true;

		return config.RequirePaintTool && !PaintToolSet.IsPaintTool(player.HeldItem) && !player.autoPaint;
	}

	/// <summary>
	/// A left click is the one button free while the picker is held open on right click, so it drives
	/// every button here.
	/// </summary>
	private static void HandleClicks(PaintWheelConfig config)
	{
		if (!freshLeftClick)
			return;

		// Editing: a click puts a colour in the palette or takes it out, or finishes.
		if (overlay == Overlay.Grid) {
			if (hoveredDone) {
				FinishEditing(config);
				Play(SoundID.MenuClose, config);
				Consume();
				return;
			}

			if (hoveredCell >= 0 && hoveredCell < editPaints.Count) {
				ToggleMember(editPaints[hoveredCell], config);
				Play(SoundID.MenuTick, config);
			}

			Consume();
			return;
		}

		if (overlay == Overlay.Palettes && hoveredPlus) {
			Play(CreatePreset(config) ? SoundID.Grab : SoundID.MenuClose, config);
			Consume();
			return;
		}

		if (overlay == Overlay.Palettes && hoveredRow >= 0 && hoveredAction >= 0) {
			MenuRow entry = menuRows[hoveredRow];
			bool done = hoveredAction == 0
				? StartEditing(entry.Preset, config)
				: DeletePreset(entry.Preset, config);

			Play(done ? SoundID.Grab : SoundID.MenuClose, config);
			Consume();
			return;
		}

		if (overlay != Overlay.None) {
			if (hoveredRow >= 0) {
				RowResult result = ActivateRow(hoveredRow, config);
				Play(result == RowResult.Failed ? SoundID.MenuClose : SoundID.Grab, config);

				if (result == RowResult.CloseAfter && !sticky)
					CloseSilently();
			}
			else {
				// Clicking off the rows backs out: to the swatches if there are any, otherwise away.
				Play(SoundID.MenuClose, config);

				if (overlay == Overlay.Scrape)
					CloseSilently();
				else
					overlay = Overlay.None;
			}

			Consume();
			return;
		}

		if (hoveredArrow >= 0) {
			ChangePage(hoveredArrow == 0 ? -1 : 1, config);
			Consume();
			return;
		}

		// The scrape button takes a click as well as a release, like every other button here.
		if (IsScrapeButton(hoveredCoating)) {
			if (EnterScrapeFromRow(config))
				Play(SoundID.Grab, config);
			else
				Play(SoundID.MenuClose, config);

			Consume();
			return;
		}

		if ((hoveredCenter || hoveredHeader) && MenuAvailable) {
			OpenOverlay(Overlay.Palettes, config);
			Consume();
			return;
		}

		// The bottom row is settings, not the choice the picker exists to make: none of it closes
		// anything, so it answers a left click while right is still held, exactly like the scraper
		// button beside it.
		if (hoveredCoating >= 0 && hoveredCoating < coatingRow.Count) {
			PaintSelection.SelectCoating(coatingRow[hoveredCoating]);
			Play(SoundID.Grab, config);
			Consume();
			return;
		}

		// A switch, not a colour, so the picker stays up and it can be flipped straight back.
		if (IsNoPaintButton(hoveredCoating)) {
			PaintSelection.ToggleNoPaint();
			Play(SoundID.Grab, config);
			Consume();
			return;
		}

		// A colour is what a held picker commits on release, so a stray left click must not take it.
		if (!sticky)
			return;

		if (hoveredSwatch >= 0 && hoveredSwatch < swatches.Count) {
			// While editing, a click is what puts a colour in or takes it out, so the picker stays up.
			if (Editing) {
				ToggleMember(swatches[hoveredSwatch], config);
				Play(SoundID.MenuTick, config);
				Consume();
				return;
			}

			// A colour is the one choice that finishes the job, so it is the one that closes.
			PaintSelection.SelectPaint(swatches[hoveredSwatch]);
			Play(SoundID.Grab, config);
			Consume();
			CloseSilently();
			return;
		}

		if (cursorMoved) {
			Play(SoundID.MenuClose, config);
			Consume();
			CloseSilently();
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
	private static bool ShouldCommit(PaintWheelConfig config)
	{
		if (!viaKeybind)
			return !Main.mouseRight;

		var key = PaintWheel.OpenWheelKey;
		if (key is null)
			return true;

		if (key.Current)
			return false;

		// Tapped rather than held: stick open instead, and let clicking do the choosing from here.
		if (framesOpen <= StickyGraceTicks) {
			sticky = true;
			return false;
		}

		return true;
	}

	/// <summary>
	/// What puts a stuck-open picker away: pressing the trigger again, or the keybind that opened it.
	/// A left click never dismisses, because left clicks are how everything in this mode is chosen.
	/// </summary>
	private static bool ShouldDismiss()
	{
		if (viaKeybind && PaintWheel.OpenWheelKey is { JustPressed: true })
			return true;

		return !viaKeybind && freshRightClick;
	}

	/// <summary>
	/// Scroll walks pages, or changes palette when there is only one page so a scroll is never answered
	/// with nothing. Shift always changes palette.
	/// </summary>
	private static void HandleScroll(PaintWheelConfig config)
	{
		int delta = PlayerInput.ScrollWheelDeltaForUI;

		// Claimed so nothing else in the UI pass acts on the same notch. The hotbar is handled in
		// PaintWheelSystem.PreUpdatePlayers, because UpdateInput overwrites this later in the tick.
		PlayerInput.ScrollWheelDelta = 0;
		PlayerInput.ScrollWheelDeltaForUI = 0;

		if (delta == 0)
			return;

		int direction = delta > 0 ? -1 : 1;
		bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);

		// Nothing to scroll through in the scrape list; the delta is still swallowed above so the
		// hotbar does not move underneath it.
		if (overlay == Overlay.Scrape)
			return;

		if (overlay == Overlay.Palettes || shift || PageCount(config) <= 1)
			CyclePalette(direction, config);
		else
			ChangePage(direction, config);
	}

	private static void UpdateHover(PaintWheelConfig config)
	{
		Vector2 cursor = Main.MouseScreen;

		if (!cursorMoved && Vector2.DistanceSquared(cursor, openCursor) > MovedThreshold * MovedThreshold)
			cursorMoved = true;

		WheelLayout.Settings settings = BuildSettings(config, WheelMath.EaseOut(anim));
		WheelLayout.Geometry geometry = WheelLayout.Compute(settings, anchor);

		// The centre and header are live immediately; swatches wait for the cursor to move, so a picker
		// opening under the cursor cannot commit what sits there.
		bool overlaid = overlay != Overlay.None;

		hoveredCenter = settings.Style == WheelLayoutStyle.Wheel && !overlaid && MenuAvailable
			&& Vector2.Distance(cursor, anchor) <= CenterHitRadius(config);

		hoveredHeader = !overlaid && MenuAvailable && settings.ShowHeader
			&& geometry.HeaderBox.Contains((int)cursor.X, (int)cursor.Y);

		if (overlay == Overlay.Grid) {
			Vector2 gridAt = GridCenter(geometry);

			hoveredDone = cursorMoved
				&& WheelLayout.GridTitleAction(gridAt, gridShape).Contains((int)cursor.X, (int)cursor.Y);

			int cell = cursorMoved && !hoveredDone
				? WheelLayout.HitTestGrid(gridAt, gridShape, cursor)
				: -1;

			if (cell != hoveredCell)
				Play(SoundID.MenuTick, config);

			hoveredCell = cell;
			hoveredRow = -1;
			hoveredSwatch = -1;
			hoveredCoating = -1;
			hoveredArrow = -1;
			return;
		}

		if (overlaid) {
			Vector2 menuAt = MenuCenter(geometry);

			int row = cursorMoved
				? WheelLayout.HitTestMenu(menuAt, menuShape, cursor)
				: -1;

			if (row != hoveredRow)
				Play(SoundID.MenuTick, config);

			hoveredRow = row;
			hoveredAction = HitTestRowActions(menuAt, cursor);
			hoveredPlus = overlay == Overlay.Palettes && cursorMoved
				&& WheelLayout.MenuTitleAction(menuAt, menuShape).Contains((int)cursor.X, (int)cursor.Y);
			hoveredSwatch = -1;
			hoveredCoating = -1;
			hoveredArrow = -1;
			return;
		}

		hoveredRow = -1;
		hoveredArrow = HitTestArrows(config, geometry, cursor);

		// An arrow no longer hides the swatch behind it. Sitting beside the swatches means a flick that
		// overshoots can land on one, and a release there has to pick the colour that was aimed at -
		// the arrow answers to a click, never to a release.
		bool blocked = !cursorMoved || hoveredCenter || hoveredHeader;

		int coating = blocked ? -1 : WheelLayout.HitTestCoating(settings, geometry, cursor);
		int swatch = blocked || coating >= 0 ? -1 : WheelLayout.HitTestSwatch(settings, geometry, cursor);

		if (swatch != hoveredSwatch || coating != hoveredCoating)
			Play(SoundID.MenuTick, config);

		hoveredSwatch = swatch;
		hoveredCoating = coating;
	}

	/// <summary>Which button on the hovered row the cursor is over, or -1 for the row itself.</summary>
	private static int HitTestRowActions(Vector2 menuAt, Vector2 cursor)
	{
		if (hoveredRow < 0 || hoveredRow >= menuRows.Count)
			return -1;

		MenuRow entry = menuRows[hoveredRow];
		int count = ActionCount(entry);
		if (count <= 0)
			return -1;

		Rectangle row = WheelLayout.MenuRow(menuAt, menuShape, hoveredRow);

		for (int i = 0; i < count; i++) {
			if (WheelLayout.MenuRowAction(row, i, count).Contains((int)cursor.X, (int)cursor.Y))
				return i;
		}

		return -1;
	}

	/// <summary>How close to the anchor counts as the centre button.</summary>
	private static float CenterHitRadius(PaintWheelConfig config)
		=> MathF.Max(20f, MathF.Min(config.Appearance.DeadZoneRadius, config.Appearance.SwatchSize * 0.5f));

	private static int HitTestArrows(PaintWheelConfig config, in WheelLayout.Geometry geometry, Vector2 cursor)
	{
		if (overlay != Overlay.None || PageCount(config) <= 1)
			return -1;

		var point = new Point((int)cursor.X, (int)cursor.Y);
		if (geometry.LeftArrow.Contains(point))
			return 0;

		return geometry.RightArrow.Contains(point) ? 1 : -1;
	}

	/// <summary>Same rule as the wheel: nothing is chosen until the cursor moves onto a row.</summary>
	private static void ResetMenuCursor()
	{
		hoveredRow = -1;
		openCursor = Main.MouseScreen;
		cursorMoved = false;
	}
}
