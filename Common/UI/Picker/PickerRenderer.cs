using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// Everything the picker draws apart from the palette list, the scrape wheel and the editor grid: the
/// wheel, the bar, the grid layout, the coating row and the text around them - and the discs the scrape
/// wheel is built from too, so the two wheels are one visual language. Nothing here decides anything -
/// it reads the state the update pass left behind.
/// </summary>
internal static class PickerRenderer
{
	// Colours only the picker uses. Those shared with the config grid and the cursor marker are UIColors.
	internal static readonly Color RimColor = new(18, 18, 28);
	private static readonly Color HaloColor = new(226, 226, 238);
	private static readonly Color UnlitPip = new(38, 38, 48);
	internal static readonly Color EmptyMark = new(232, 96, 96);
	internal static readonly Color MenuRowTint = new(72, 76, 160);
	private static readonly Color EmptyDiscFill = new(52, 52, 64);
	private static readonly Color ScrapeDiscFill = new(64, 58, 48);

	/// <summary>The disc in the middle of a wheel, when it is a button rather than your colour.</summary>
	internal static readonly Color CenterDiscFill = new(46, 48, 78);

	public static void Draw(SpriteBatch spriteBatch)
	{
		if (PaintPicker.Anim <= 0f)
			return;

		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null)
			return;

		float opacity = PaintPicker.Anim;
		float eased = WheelMath.EaseOut(PaintPicker.Anim);

		WheelLayout.Settings settings = PaintPicker.BuildSettings(config, eased);
		WheelLayout.Geometry geometry = WheelLayout.Compute(settings, PaintPicker.Anchor);

		// The scrape wheel is its own shape, with its own panel, title and name.
		bool scraping = PaintPicker.DrawOverlay == PickerOverlay.Scrape;

		if (config.Appearance.ShowBackgroundPanel && !scraping)
			WheelDrawing.DrawPanel(spriteBatch, geometry.Panel, opacity);

		// Only over the swatches: a list carries its own title and marks its own current row, so drawing
		// the header too says everything twice.
		if (PaintPicker.DrawOverlay == PickerOverlay.None)
			DrawHeader(spriteBatch, config, geometry, opacity);

		// Swatches are put away rather than faded: the menu is wider than the ring's clear space, so a
		// ghost ring behind it is noise.
		if (PaintPicker.DrawOverlay == PickerOverlay.None) {
			switch (settings.Style) {
				case WheelLayoutStyle.Bar:
					DrawBar(spriteBatch, config, settings, geometry, opacity, eased);
					break;

				case WheelLayoutStyle.Grid:
					DrawGridSwatches(spriteBatch, config, settings, geometry, opacity);
					break;

				default:
					DrawWheel(spriteBatch, config, settings, geometry, opacity, eased);
					break;
			}
		}

		// No coating row in scrape mode, where coatings mean nothing, nor under the editor grid, which
		// covers it and takes every click.
		if (PaintPicker.DrawOverlay is not (PickerOverlay.Scrape or PickerOverlay.Grid)) {
			DrawCoatingRow(spriteBatch, config, settings, geometry,
				PaintPicker.DrawOverlay == PickerOverlay.Palettes ? opacity * 0.35f : opacity, eased);
		}

		if (PaintPicker.DrawOverlay == PickerOverlay.Grid)
			PaletteEditor.DrawGrid(spriteBatch, geometry, opacity);
		else if (scraping)
			ScrapeWheel.Draw(spriteBatch, config, settings, opacity);
		else if (PaintPicker.DrawOverlay == PickerOverlay.Palettes)
			PickerMenu.DrawMenu(spriteBatch, geometry, opacity);
		else
			DrawHoverText(spriteBatch, config, settings, geometry, opacity);
	}

	private static void DrawHeader(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Geometry geometry, float opacity)
	{
		string label = PaintPicker.HeaderLabel(config);
		int pages = PickerContent.PageCount(config);

		if (label is null || (!PaintPicker.MenuAvailable && pages <= 1))
			return;

		Color color = PickerInput.HoveredHeader ? Color.White : Main.OurFavoriteColor;
		WheelDrawing.DrawTextCentered(spriteBatch, label, geometry.HeaderCenter, color, opacity, PaintPicker.HeaderTextScale);

		// Drawn, not a character: the UI font has no arrow and would render a missing-glyph box.
		if (PaintPicker.MenuAvailable) {
			float half = WheelDrawing.MeasureText(label, PaintPicker.HeaderTextScale).X * 0.5f;
			var at = new Vector2(geometry.HeaderCenter.X + half + 9f, geometry.HeaderCenter.Y + 1f);

			WheelDrawing.DrawTriangleVertical(spriteBatch, at + Vector2.One, 9f, 1, Color.Black * (opacity * 0.6f));
			WheelDrawing.DrawTriangleVertical(spriteBatch, at, 9f, 1, color * opacity);
		}

		if (pages <= 1)
			return;

		DrawArrow(spriteBatch, geometry.LeftArrow, -1, PickerInput.HoveredArrow == 0, opacity);
		DrawArrow(spriteBatch, geometry.RightArrow, 1, PickerInput.HoveredArrow == 1, opacity);
	}

	/// <summary>A disc behind the triangle, so the click target has a visible size.</summary>
	private static void DrawArrow(SpriteBatch spriteBatch, Rectangle box, int direction, bool hovered, float opacity)
	{
		Vector2 center = box.Center.ToVector2();
		float radius = box.Width * 0.5f;

		WheelDrawing.DrawDisc(spriteBatch, center, radius,
			(hovered ? HaloColor : RimColor) * (opacity * (hovered ? 0.95f : 0.75f)));
		WheelDrawing.DrawDisc(spriteBatch, center, radius - 2f,
			(hovered ? MenuRowTint : new Color(30, 30, 42)) * opacity);

		float size = radius * (hovered ? 1.1f : 0.95f);
		WheelDrawing.DrawTriangle(spriteBatch, center + new Vector2(0f, 1f), size, direction,
			Color.Black * (opacity * 0.5f));
		WheelDrawing.DrawTriangle(spriteBatch, center, size, direction,
			(hovered ? Color.White : new Color(206, 210, 232)) * opacity);
	}

	// ---- Wheel ------------------------------------------------------------------------------

	private static void DrawWheel(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, float opacity, float eased)
	{
		for (int i = 0; i < PickerContent.Swatches.Count; i++) {
			if (i != PickerInput.HoveredSwatch)
				DrawDiscSwatch(spriteBatch, config, settings, geometry, i, opacity, eased);
		}

		// Last, so the hover pop is never clipped by the neighbour drawn after it.
		if (PickerInput.HoveredSwatch >= 0 && PickerInput.HoveredSwatch < PickerContent.Swatches.Count)
			DrawDiscSwatch(spriteBatch, config, settings, geometry, PickerInput.HoveredSwatch, opacity, eased);

		DrawWheelCenter(spriteBatch, config, opacity, eased);
	}

	private static void DrawDiscSwatch(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, int index, float opacity, float eased)
	{
		int type = PickerContent.Swatches[index];
		int total = index < PickerContent.SwatchStacks.Count ? PickerContent.SwatchStacks[index] : 0;

		// Only one thing looks hovered at a time: over an arrow, the arrow is what a click would hit.
		bool hovered = index == PickerInput.HoveredSwatch && PickerInput.HoveredArrow < 0;

		Vector2 center = WheelLayout.SwatchCenter(settings, geometry, index);
		float radius = config.Appearance.SwatchSize * 0.5f * eased * (hovered ? WheelLayout.HoverScale : 1f);

		if (ShowGauge(config, total)) {
			WheelDrawing.DrawSupplyRing(spriteBatch, center, radius + WheelLayout.GaugeOffset * eased,
				config.Supply.DepletionRingSegments, SupplyFill(config, total),
				WheelLayout.GaugePip(config.Appearance.SwatchSize) * eased,
				PaintCatalog.AccentColor(type) * opacity, UnlitPip * opacity);
		}

		DrawDisc(spriteBatch, config, center, radius, type, total, hovered, Marked(type), opacity);

		// The key and the count sit where the hotbar puts them on a slot: top left and bottom right.
		DrawKeyHint(spriteBatch, config, index, center + new Vector2(-radius * 0.95f, -radius * 1.05f), opacity);

		if (config.Appearance.ShowStackCounts && total > 0) {
			string count = total.ToString();
			Vector2 size = WheelDrawing.MeasureSmallNumber(count, CountScale);
			WheelDrawing.DrawSmallNumber(spriteBatch, count, center + new Vector2(radius * 0.95f - size.X, radius * 0.95f - size.Y),
				Color.White * opacity, CountScale);
		}
	}

	private const float CountScale = 0.62f;

	/// <summary>
	/// The number key that picks this swatch while the picker is up - 1 to 9 and 0 for the first ten -
	/// dimmed, so it reads as a hint rather than as part of the colour.
	/// </summary>
	internal static void DrawKeyHint(SpriteBatch spriteBatch, PaintWheelConfig config, int index, Vector2 at, float opacity)
	{
		if (!config.Appearance.ShowQuickKeys || index >= 10)
			return;

		WheelDrawing.DrawSmallNumber(spriteBatch, ((index + 1) % 10).ToString(), at, new Color(230, 232, 244) * (opacity * 0.8f), 0.58f);
	}

	/// <summary>
	/// Halo, rim, fill: the three layers every disc here is built from. The pale halo matters - without
	/// it a Shadow Paint disc vanishes into a dark background, since nothing is drawn behind the picker.
	/// </summary>
	internal static void DrawDiscBody(SpriteBatch spriteBatch, Vector2 center, float radius, Color fill,
		bool hovered, float opacity)
	{
		WheelDrawing.DrawDisc(spriteBatch, center, radius + 1f, HaloColor * (opacity * (hovered ? 0.75f : 0.4f)));
		WheelDrawing.DrawDisc(spriteBatch, center, radius, RimColor * opacity);
		WheelDrawing.DrawDisc(spriteBatch, center, radius - 2f, fill * opacity);
	}

	internal static void DrawDiscMarks(SpriteBatch spriteBatch, Vector2 center, float radius, bool chosen,
		bool hovered, float opacity)
	{
		if (chosen)
			WheelDrawing.DrawDiscOutline(spriteBatch, center, radius + 3f, 2f, UIColors.Chosen * opacity);

		if (hovered)
			WheelDrawing.DrawDiscOutline(spriteBatch, center, radius + 1f, 2f, Color.White * opacity);
	}

	private static void DrawDisc(SpriteBatch spriteBatch, PaintWheelConfig config, Vector2 center,
		float radius, int type, int total, bool hovered, bool chosen, float opacity)
	{
		DrawDiscBody(spriteBatch, center, radius, SwatchFill(config, type, total, hovered), hovered, opacity);
		DrawDiscMarks(spriteBatch, center, radius, chosen, hovered, opacity);

		if (config.Appearance.ShowItemIcons && type > 0)
			WheelDrawing.DrawItemIcon(spriteBatch, type, center, radius / 22f,
				Color.White * (opacity * (total > 0 ? 1f : 0.3f)));

		if (total <= 0 && type > 0)
			WheelDrawing.DrawSlash(spriteBatch, center, radius * 1.1f, EmptyMark * opacity);
	}

	/// <summary>
	/// The dead zone shows the current selection and doubles as the palette button - it is the one part
	/// of the wheel that never selects a paint.
	/// </summary>
	private static void DrawWheelCenter(SpriteBatch spriteBatch, PaintWheelConfig config, float opacity, float eased)
	{
		int chosen = PaintSelection.Paint;
		int coating = PaintSelection.Coating;

		// Drawn as a button whenever it is one - the same rule the hit test uses - so a click that opens
		// the palette list always had something visible to land on.
		bool button = PaintPicker.MenuAvailable;

		if (chosen == 0 && coating <= 0 && !button)
			return;

		// Close to the hit radius, so the button is never smaller than it looks.
		float radius = MathF.Max(config.Appearance.SwatchSize * 0.4f, 16f) * eased;

		if (chosen > 0) {
			int total = PaintInventory.TotalStack(Main.LocalPlayer, chosen);
			DrawDiscBody(spriteBatch, PaintPicker.Anchor, radius, SwatchFill(config, chosen, total, false), false, opacity);
		}
		else if (chosen == PaintSelection.NoPaint) {
			// Bare placement is a choice like a colour, so the centre shows it the way the bottom row does.
			DrawDiscBody(spriteBatch, PaintPicker.Anchor, radius, EmptyDiscFill, false, opacity);
			WheelDrawing.DrawItemIcon(spriteBatch, ItemID.Paintbrush, PaintPicker.Anchor, radius / 22f,
				Color.White * (opacity * 0.55f));
			WheelDrawing.DrawSlash(spriteBatch, PaintPicker.Anchor, radius * 1.15f, new Color(236, 132, 132) * opacity);
		}
		else if (button) {
			WheelDrawing.DrawDisc(spriteBatch, PaintPicker.Anchor, radius, RimColor * (opacity * 0.85f));
			WheelDrawing.DrawDisc(spriteBatch, PaintPicker.Anchor, radius - 2f, CenterDiscFill * opacity);
		}

		if (button) {
			WheelDrawing.DrawDiscOutline(spriteBatch, PaintPicker.Anchor, radius + 2f, PickerInput.HoveredCenter ? 2f : 1f,
				(PickerInput.HoveredCenter ? Color.White : HaloColor * 0.5f) * opacity);

			// Three pips only while hovered, so the resting state stays a clean read of your colour.
			if (PickerInput.HoveredCenter) {
				float spacing = radius * 0.44f;
				float pip = MathF.Max(2f, radius * 0.17f);

				for (int i = -1; i <= 1; i++)
					WheelDrawing.DrawPixelSquare(spriteBatch, PaintPicker.Anchor + new Vector2(i * spacing, 0f), pip,
						Color.White * opacity);
			}
		}

		if (coating > 0) {
			Vector2 badge = PaintPicker.Anchor + new Vector2(radius, radius) * 0.95f;
			float small = MathF.Max(4f, radius * 0.6f);

			WheelDrawing.DrawDisc(spriteBatch, badge, small, RimColor * opacity);
			WheelDrawing.DrawDisc(spriteBatch, badge, small - 2f, PaintCatalog.AccentColor(coating) * opacity);
		}
	}

	// ---- Grid -------------------------------------------------------------------------------

	/// <summary>
	/// Every swatch in rows, laid out like the config's palette grid and the editor, so the three
	/// places colours are shown all read the same way. Nothing here pages: that is the point of it.
	/// </summary>
	private static void DrawGridSwatches(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, float opacity)
	{
		for (int i = 0; i < PickerContent.Swatches.Count; i++) {
			int type = PickerContent.Swatches[i];
			int total = i < PickerContent.SwatchStacks.Count ? PickerContent.SwatchStacks[i] : 0;
			bool hovered = i == PickerInput.HoveredSwatch && PickerInput.HoveredArrow < 0;

			Rectangle cell = WheelLayout.GridCell(geometry.Anchor, geometry.Cells, i);

			WheelDrawing.DrawPaletteCell(spriteBatch, cell, SwatchFill(config, type, total, hovered), Marked(type),
				hovered, null, opacity);

			if (config.Appearance.ShowItemIcons)
				WheelDrawing.DrawItemIcon(spriteBatch, type, SwatchCenterOf(cell), cell.Width / 30f,
					Color.White * (opacity * (total > 0 ? 1f : 0.3f)));

			if (total <= 0)
				WheelDrawing.DrawSlash(spriteBatch, SwatchCenterOf(cell), cell.Width * 0.62f, EmptyMark * opacity);

			if (ShowGauge(config, total)) {
				var gauge = new Rectangle(cell.X + 2, cell.Bottom - 4, Math.Max(4, cell.Width - 4), 2);

				WheelDrawing.DrawSupplyBar(spriteBatch, gauge, config.Supply.DepletionRingSegments,
					SupplyFill(config, total), Color.White * (opacity * 0.8f), Color.Black * (opacity * 0.45f));
			}

			// After the gauge, so the count is drawn over it rather than under.
			if (config.Appearance.ShowStackCounts && total > 0)
				WheelDrawing.DrawCellCorner(spriteBatch, cell, total.ToString(), opacity);

			DrawKeyHint(spriteBatch, config, i, new Vector2(cell.X + 2f, cell.Y + 1f), opacity);
		}
	}

	private static Vector2 SwatchCenterOf(Rectangle cell)
		=> new(cell.X + cell.Width * 0.5f, cell.Y + cell.Height * 0.5f);

	// ---- Bar --------------------------------------------------------------------------------

	private static void DrawBar(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, float opacity, float eased)
	{
		if (PickerContent.Swatches.Count == 0)
			return;

		WheelDrawing.DrawRectOutline(spriteBatch, geometry.Strip, 2, RimColor * opacity);

		for (int i = 0; i < PickerContent.Swatches.Count; i++)
			DrawBarCell(spriteBatch, config, settings, geometry, i, opacity);
	}

	private static void DrawBarCell(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, int index, float opacity)
	{
		int type = PickerContent.Swatches[index];
		int total = index < PickerContent.SwatchStacks.Count ? PickerContent.SwatchStacks[index] : 0;
		bool hovered = index == PickerInput.HoveredSwatch && PickerInput.HoveredArrow < 0;

		Vector2 center = WheelLayout.SwatchCenter(settings, geometry, index);
		int height = Math.Max(2, (int)MathF.Round(settings.CellHeight));

		var cell = new Rectangle(
			geometry.Strip.X,
			(int)MathF.Round(center.Y - height * 0.5f),
			geometry.Strip.Width + (hovered ? 12 : 0),
			height);

		WheelDrawing.DrawRect(spriteBatch, cell, SwatchFill(config, type, total, hovered) * opacity);

		// A one pixel dark seam between bands, the way a printed palette strip reads.
		WheelDrawing.DrawRect(spriteBatch, new Rectangle(cell.X, cell.Bottom - 1, cell.Width, 1),
			RimColor * (opacity * 0.8f));

		if (ShowGauge(config, total)) {
			var gauge = new Rectangle(cell.X + 3, cell.Bottom - 5, Math.Max(4, cell.Width - 6), 2);
			WheelDrawing.DrawSupplyBar(spriteBatch, gauge, config.Supply.DepletionRingSegments, SupplyFill(config, total),
				Color.White * (opacity * 0.8f), Color.Black * (opacity * 0.45f));
		}

		if (config.Appearance.ShowItemIcons)
			WheelDrawing.DrawItemIcon(spriteBatch, type, new Vector2(cell.X + 13f, center.Y),
				settings.CellHeight / 30f, Color.White * (opacity * (total > 0 ? 1f : 0.3f)));

		if (total <= 0)
			WheelDrawing.DrawSlash(spriteBatch, new Vector2(cell.Center.X, center.Y),
				settings.CellHeight * 0.7f, EmptyMark * opacity);

		if (Marked(type))
			WheelDrawing.DrawTriangle(spriteBatch, new Vector2(cell.X - 10f, center.Y),
				settings.CellHeight * 0.55f, 1, UIColors.Chosen * opacity);

		DrawKeyHint(spriteBatch, config, index, new Vector2(cell.X + 3f, cell.Y + 1f), opacity);

		if (config.Appearance.ShowStackCounts && total > 0) {
			string count = total.ToString();
			Vector2 size = WheelDrawing.MeasureSmallNumber(count, CountScale);
			WheelDrawing.DrawSmallNumber(spriteBatch, count, new Vector2(cell.Right - size.X - 3f, center.Y - size.Y * 0.5f),
				Color.White * opacity, CountScale);
		}

		if (hovered) {
			WheelDrawing.DrawRectOutline(spriteBatch, cell, 1, RimColor * opacity);
			WheelDrawing.DrawRectOutline(spriteBatch, new Rectangle(cell.X - 1, cell.Y - 1, cell.Width + 2, cell.Height + 2),
				1, Color.White * opacity);
		}
	}

	// ---- Bottom row and hover text ----------------------------------------------------------

	private static void DrawCoatingRow(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, float opacity, float eased)
	{
		Player player = Main.LocalPlayer;
		int chosen = PaintSelection.Coating;

		for (int i = 0; i < PickerContent.RowSlots; i++) {
			bool hovered = i == PickerInput.HoveredCoating;

			if (PickerContent.IsScrapeButton(i) || PickerContent.IsNoPaintButton(i) || PickerContent.IsPaintBothButton(i)) {
				Vector2 buttonCenter = WheelLayout.CoatingCenter(settings, geometry, i);
				float buttonRadius = geometry.CoatingSize * 0.5f * eased * (hovered ? WheelLayout.HoverScale : 1f);

				if (PickerContent.IsScrapeButton(i))
					DrawScrapeButton(spriteBatch, buttonCenter, buttonRadius, hovered, opacity);
				else if (PickerContent.IsPaintBothButton(i))
					DrawPaintBothButton(spriteBatch, buttonCenter, buttonRadius, hovered, opacity);
				else
					DrawNoPaintButton(spriteBatch, buttonCenter, buttonRadius, hovered, opacity);

				continue;
			}

			int type = PickerContent.CoatingRow[i];
			int total = type > 0 ? PaintInventory.TotalStack(player, type) : 1;

			Vector2 center = WheelLayout.CoatingCenter(settings, geometry, i);
			float radius = geometry.CoatingSize * 0.5f * eased * (hovered ? WheelLayout.HoverScale : 1f);

			// The "no coating" slot: a crossed-out disc reads better than an empty one.
			if (type <= 0) {
				DrawDiscBody(spriteBatch, center, radius, EmptyDiscFill, hovered, opacity);
				DrawDiscMarks(spriteBatch, center, radius, type == chosen, hovered, opacity);
				WheelDrawing.DrawCross(spriteBatch, center, radius * 0.9f, new Color(198, 198, 210) * opacity);
				continue;
			}

			DrawDisc(spriteBatch, config, center, radius, type, total, hovered, type == chosen, opacity);
		}
	}

	/// <summary>
	/// The scrape button: the scraper's own sprite in a disc, so it reads as a tool rather than as
	/// another colour to pick.
	/// </summary>
	private static void DrawScrapeButton(SpriteBatch spriteBatch, Vector2 center, float radius,
		bool hovered, float opacity)
	{
		DrawDiscBody(spriteBatch, center, radius, ScrapeDiscFill, hovered, opacity);
		DrawDiscMarks(spriteBatch, center, radius, chosen: false, hovered, opacity);

		int slot = PaintScraper.FindSlot(Main.LocalPlayer);
		int type = slot >= 0 ? Main.LocalPlayer.inventory[slot].type : ItemID.PaintScraper;

		WheelDrawing.DrawItemIcon(spriteBatch, type, center, radius / 20f, Color.White * opacity);
	}

	/// <summary>
	/// The paint-both switch, in its own art: at full strength with the gold ring while on, faded while
	/// off - so the state reads at a glance without hovering.
	/// </summary>
	private static void DrawPaintBothButton(SpriteBatch spriteBatch, Vector2 center, float radius,
		bool hovered, float opacity)
	{
		bool on = PaintSelection.PaintBoth;

		DrawDiscBody(spriteBatch, center, radius, EmptyDiscFill, hovered, opacity);
		DrawDiscMarks(spriteBatch, center, radius, on, hovered, opacity);
		WheelDrawing.DrawIcon(spriteBatch, UITextures.PaintBoth, center, radius * 1.3f,
			Color.White * (opacity * (on ? 1f : hovered ? 0.75f : 0.45f)));
	}

	/// <summary>
	/// Bare placement: a brush struck through. A cross would read as the neighbouring "no coating" slot,
	/// so the tool being refused is drawn instead of another empty disc.
	/// </summary>
	private static void DrawNoPaintButton(SpriteBatch spriteBatch, Vector2 center, float radius,
		bool hovered, float opacity)
	{
		DrawDiscBody(spriteBatch, center, radius, EmptyDiscFill, hovered, opacity);
		DrawDiscMarks(spriteBatch, center, radius, PaintSelection.Paint == PaintSelection.NoPaint, hovered, opacity);

		WheelDrawing.DrawItemIcon(spriteBatch, ItemID.Paintbrush, center, radius / 22f,
			Color.White * (opacity * 0.55f));
		WheelDrawing.DrawSlash(spriteBatch, center, radius * 1.15f, new Color(236, 132, 132) * opacity);
	}

	/// <summary>
	/// What the gold ring means on a swatch: the paint you are using. (In the editor grid it marks the
	/// palette's members instead, which PaletteEditor draws itself.)
	/// </summary>
	private static bool Marked(int type) => type == PaintSelection.Paint;

	private static void DrawHoverText(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, float opacity)
	{
		string text = HoverText(config);
		if (text is null)
			return;

		if (settings.Style != WheelLayoutStyle.Bar) {
			WheelDrawing.DrawTextCentered(spriteBatch, text, geometry.InfoAnchor, Color.White, opacity, 0.9f);
			return;
		}

		float y = PickerInput.HoveredSwatch >= 0 && PickerInput.HoveredSwatch < PickerContent.Swatches.Count
			? WheelLayout.SwatchCenter(settings, geometry, PickerInput.HoveredSwatch).Y
			: geometry.InfoAnchor.Y;

		// Past the page arrow when the name is level with it - it sits by the middle of the strip.
		float x = geometry.Strip.Right + 18f;
		if (settings.Paged && MathF.Abs(y - geometry.RightArrow.Center.Y) < WheelLayout.ArrowSize)
			x = MathF.Max(x, geometry.RightArrow.Right + 6f);

		WheelDrawing.DrawTextLeft(spriteBatch, text, new Vector2(x, y), Color.White, opacity, 0.9f);
	}

	private static string HoverText(PaintWheelConfig config)
	{
		// With one palette there is nothing to switch to yet; the list is where the first one gets made.
		if (PickerInput.HoveredCenter || PickerInput.HoveredHeader) {
			return Language.GetTextValue(PickerContent.Palettes.Count > 1
				? "Mods.PaintWheel.UI.PaletteHint"
				: "Mods.PaintWheel.UI.PaletteHintNew");
		}

		if (PickerInput.HoveredArrow >= 0)
			return null;

		if (PickerContent.IsScrapeButton(PickerInput.HoveredCoating))
			return Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Enter");

		if (PickerContent.IsPaintBothButton(PickerInput.HoveredCoating)) {
			return Language.GetTextValue(PaintSelection.PaintBoth
				? "Mods.PaintWheel.UI.PaintBothResume"
				: "Mods.PaintWheel.UI.PaintBoth");
		}

		if (PickerContent.IsNoPaintButton(PickerInput.HoveredCoating)) {
			return Language.GetTextValue(PaintSelection.Paint == PaintSelection.NoPaint
				? "Mods.PaintWheel.UI.NoPaintResume"
				: "Mods.PaintWheel.UI.NoPaint");
		}

		if (PickerInput.HoveredCoating >= 0 && PickerInput.HoveredCoating < PickerContent.CoatingRow.Count) {
			int type = PickerContent.CoatingRow[PickerInput.HoveredCoating];
			return type <= 0
				? Language.GetTextValue("Mods.PaintWheel.UI.NoCoating")
				: Describe(type, PaintInventory.TotalStack(Main.LocalPlayer, type));
		}

		if (PickerInput.HoveredSwatch >= 0 && PickerInput.HoveredSwatch < PickerContent.Swatches.Count) {
			int total = PickerInput.HoveredSwatch < PickerContent.SwatchStacks.Count ? PickerContent.SwatchStacks[PickerInput.HoveredSwatch] : 0;
			return Describe(PickerContent.Swatches[PickerInput.HoveredSwatch], total);
		}

		// Holding a block with nothing to paint it: say so, rather than letting the pick quietly do
		// nothing when the block goes down.
		Player player = Main.LocalPlayer;
		if (!PaintToolSet.AutoPaints(player) && !PaintToolSet.IsPaintTool(player.HeldItem) && PaintToolSet.IsPlaceable(player.HeldItem))
			return Language.GetTextValue("Mods.PaintWheel.UI.NoAutoPaint");

		// Instructions only where they cost nothing. The wheel keeps a clear strip under the ring for
		// the hovered paint's name, so a hint sits in space that is already reserved; beside a bar or
		// under a grid it is one more thing on screen every time the picker opens.
		if (config.Layout != WheelLayoutStyle.Wheel)
			return null;

		if (PickerContent.PageCount(config) > 1)
			return Language.GetTextValue("Mods.PaintWheel.UI.ScrollHint");

		return PickerContent.Palettes.Count > 1 ? Language.GetTextValue("Mods.PaintWheel.UI.PaletteScrollHint") : null;
	}

	private static string Describe(int type, int total)
		=> total > 0
			? $"{Lang.GetItemNameValue(type)}   {total}"
			: $"{Lang.GetItemNameValue(type)}   {Language.GetTextValue("Mods.PaintWheel.UI.Empty")}";

	/// <summary>Only shown once a stack is running down; a full gauge on every swatch says nothing.</summary>
	private static bool ShowGauge(PaintWheelConfig config, int total)
		=> config.Supply.ShowDepletionRing && SupplyFill(config, total) < 1f;

	private static float SupplyFill(PaintWheelConfig config, int total)
		=> config.Supply.FadeStartStack <= 0 ? 1f : MathHelper.Clamp(total / (float)config.Supply.FadeStartStack, 0f, 1f);

	/// <summary>
	/// The paint's real colour, dimmed toward the background as the stack runs low. One you are out of
	/// drains toward grey but stays visible, so a preset never reads as blank.
	/// </summary>
	private static Color SwatchFill(PaintWheelConfig config, int type, int total, bool hovered)
	{
		Color accent = PaintCatalog.AccentColor(type);

		if (total <= 0)
			return Color.Lerp(accent, new Color(58, 58, 70), 0.68f);

		float alpha = WheelMath.SwatchAlpha(total, config.Supply.FadeStartStack, config.Supply.MinimumSwatchOpacity);
		Color faded = Color.Lerp(new Color(44, 44, 56), accent, alpha);

		return hovered ? Color.Lerp(faded, Color.White, 0.18f) : faded;
	}
}
