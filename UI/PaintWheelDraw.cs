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
/// Everything the picker draws: the wheel, the bar, the coating row, the palette menu and the text
/// around them. Nothing here decides anything - it reads the state the update pass left behind.
/// </summary>
public static partial class PaintWheelState
{
	// ---- Draw -------------------------------------------------------------------------------

	private static readonly Color RimColor = new(18, 18, 28);
	private static readonly Color HaloColor = new(226, 226, 238);
	private static readonly Color SelectedColor = new(255, 216, 122);
	private static readonly Color UnlitPip = new(38, 38, 48);
	private static readonly Color EmptyMark = new(232, 96, 96);
	private static readonly Color MenuRowTint = new(72, 76, 160);
	private static readonly Color EmptyDiscFill = new(52, 52, 64);
	private static readonly Color ScrapeDiscFill = new(64, 58, 48);

	// Matching the config's palette grid, so the two read as the same control.
	private static readonly Color CellRim = new(20, 20, 30);
	private static readonly Color ChosenRim = new(255, 216, 122);

	public static void Draw(SpriteBatch spriteBatch)
	{
		if (anim <= 0f)
			return;

		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null)
			return;

		if (active)
			Main.LocalPlayer.mouseInterface = true;

		float opacity = anim;
		float eased = WheelMath.EaseOut(anim);

		WheelLayout.Settings settings = BuildSettings(config, eased);
		WheelLayout.Geometry geometry = WheelLayout.Compute(settings, anchor);

		if (config.Appearance.ShowBackgroundPanel)
			WheelDrawing.DrawPanel(spriteBatch, geometry.Panel, opacity);

		// Only over the swatches: a list carries its own title and marks its own current row, so drawing
		// the header too says everything twice.
		if (drawOverlay == Overlay.None)
			DrawHeader(spriteBatch, config, geometry, opacity);

		// Swatches are put away rather than faded: the menu is wider than the ring's clear space, so a
		// ghost ring behind it is noise.
		if (drawOverlay == Overlay.None) {
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

		// No coating row in scrape mode: coatings are a painting concern and it is not painting.
		if (drawOverlay != Overlay.Scrape) {
			DrawCoatingRow(spriteBatch, config, settings, geometry,
				drawOverlay == Overlay.Palettes ? opacity * 0.35f : opacity, eased);
		}

		if (drawOverlay == Overlay.Grid)
			DrawGrid(spriteBatch, geometry, opacity);
		else if (drawOverlay != Overlay.None)
			DrawMenu(spriteBatch, geometry, opacity);
		else
			DrawHoverText(spriteBatch, config, settings, geometry, opacity);
	}

	private static void DrawHeader(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Geometry geometry, float opacity)
	{
		string label = HeaderLabel(config);
		int pages = PageCount(config);

		if (label is null || (!MenuAvailable && pages <= 1))
			return;

		Color color = hoveredHeader ? Color.White : Main.OurFavoriteColor;
		WheelDrawing.DrawTextCentered(spriteBatch, label, geometry.HeaderCenter, color, opacity, HeaderTextScale);

		// Drawn, not a character: the UI font has no arrow and would render a missing-glyph box.
		if (MenuAvailable) {
			float half = WheelDrawing.MeasureText(label, HeaderTextScale).X * 0.5f;
			var at = new Vector2(geometry.HeaderCenter.X + half + 9f, geometry.HeaderCenter.Y + 1f);

			WheelDrawing.DrawTriangleVertical(spriteBatch, at + Vector2.One, 9f, 1, Color.Black * (opacity * 0.6f));
			WheelDrawing.DrawTriangleVertical(spriteBatch, at, 9f, 1, color * opacity);
		}

		if (pages <= 1)
			return;

		DrawArrow(spriteBatch, geometry.LeftArrow, -1, hoveredArrow == 0, opacity);
		DrawArrow(spriteBatch, geometry.RightArrow, 1, hoveredArrow == 1, opacity);
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
		for (int i = 0; i < swatches.Count; i++) {
			if (i != hoveredSwatch)
				DrawDiscSwatch(spriteBatch, config, settings, geometry, i, opacity, eased);
		}

		// Last, so the hover pop is never clipped by the neighbour drawn after it.
		if (hoveredSwatch >= 0 && hoveredSwatch < swatches.Count)
			DrawDiscSwatch(spriteBatch, config, settings, geometry, hoveredSwatch, opacity, eased);

		DrawWheelCenter(spriteBatch, config, opacity, eased);
	}

	private static void DrawDiscSwatch(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, int index, float opacity, float eased)
	{
		int type = swatches[index];
		int total = index < swatchStacks.Count ? swatchStacks[index] : 0;

		// Only one thing looks hovered at a time: over an arrow, the arrow is what a click would hit.
		bool hovered = index == hoveredSwatch && hoveredArrow < 0;

		Vector2 center = WheelLayout.SwatchCenter(settings, geometry, index);
		float radius = config.Appearance.SwatchSize * 0.5f * eased * (hovered ? WheelLayout.HoverScale : 1f);

		if (ShowGauge(config, total)) {
			WheelDrawing.DrawSupplyRing(spriteBatch, center, radius + WheelLayout.GaugeOffset * eased,
				config.Supply.DepletionRingSegments, SupplyFill(config, total),
				WheelLayout.GaugePip(config.Appearance.SwatchSize) * eased,
				PaintCatalog.AccentColor(type) * opacity, UnlitPip * opacity);
		}

		DrawDisc(spriteBatch, config, center, radius, type, total, hovered, Marked(type), opacity);
	}

	/// <summary>
	/// Halo, rim, fill: the three layers every disc here is built from. The pale halo matters - without
	/// it a Shadow Paint disc vanishes into a dark background, since nothing is drawn behind the picker.
	/// </summary>
	private static void DrawDiscBody(SpriteBatch spriteBatch, Vector2 center, float radius, Color fill,
		bool hovered, float opacity)
	{
		WheelDrawing.DrawDisc(spriteBatch, center, radius + 1f, HaloColor * (opacity * (hovered ? 0.75f : 0.4f)));
		WheelDrawing.DrawDisc(spriteBatch, center, radius, RimColor * opacity);
		WheelDrawing.DrawDisc(spriteBatch, center, radius - 2f, fill * opacity);
	}

	private static void DrawDiscMarks(SpriteBatch spriteBatch, Vector2 center, float radius, bool chosen,
		bool hovered, float opacity)
	{
		if (chosen)
			WheelDrawing.DrawDiscOutline(spriteBatch, center, radius + 3f, 2f, SelectedColor * opacity);

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
		bool button = sources.Count > 1;

		if (chosen <= 0 && coating <= 0 && !button)
			return;

		// Close to the hit radius, so the button is never smaller than it looks.
		float radius = MathF.Max(config.Appearance.SwatchSize * 0.4f, 16f) * eased;

		if (chosen > 0) {
			int total = PaintInventory.TotalStack(Main.LocalPlayer, chosen);
			DrawDiscBody(spriteBatch, anchor, radius, SwatchFill(config, chosen, total, false), false, opacity);
		}
		else if (button) {
			WheelDrawing.DrawDisc(spriteBatch, anchor, radius, RimColor * (opacity * 0.85f));
			WheelDrawing.DrawDisc(spriteBatch, anchor, radius - 2f, new Color(46, 48, 78) * opacity);
		}

		if (button) {
			WheelDrawing.DrawDiscOutline(spriteBatch, anchor, radius + 2f, hoveredCenter ? 2f : 1f,
				(hoveredCenter ? Color.White : HaloColor * 0.5f) * opacity);

			// Three pips only while hovered, so the resting state stays a clean read of your colour.
			if (hoveredCenter) {
				float spacing = radius * 0.44f;
				float pip = MathF.Max(2f, radius * 0.17f);

				for (int i = -1; i <= 1; i++)
					WheelDrawing.DrawPixelSquare(spriteBatch, anchor + new Vector2(i * spacing, 0f), pip,
						Color.White * opacity);
			}
		}

		if (coating > 0) {
			Vector2 badge = anchor + new Vector2(radius, radius) * 0.95f;
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
		for (int i = 0; i < swatches.Count; i++) {
			int type = swatches[i];
			int total = i < swatchStacks.Count ? swatchStacks[i] : 0;
			bool hovered = i == hoveredSwatch && hoveredArrow < 0;

			Rectangle cell = WheelLayout.GridCell(geometry.Anchor, geometry.Cells, i);

			WheelDrawing.DrawRect(spriteBatch, Inflate(cell, 1), Color.Black * (opacity * 0.45f));
			WheelDrawing.DrawRect(spriteBatch, cell, SwatchFill(config, type, total, hovered) * opacity);

			if (Marked(type))
				WheelDrawing.DrawRectOutline(spriteBatch, cell, 2, SelectedColor * opacity);
			else
				WheelDrawing.DrawRectOutline(spriteBatch, cell, 1, CellRim * (opacity * 0.8f));

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

			if (hovered) {
				WheelDrawing.DrawRectOutline(spriteBatch, Inflate(cell, 2), 2, Color.White * opacity);
				WheelDrawing.DrawRectOutline(spriteBatch, cell, 1, Color.Black * (opacity * 0.5f));
			}
		}
	}

	private static Vector2 SwatchCenterOf(Rectangle cell)
		=> new(cell.X + cell.Width * 0.5f, cell.Y + cell.Height * 0.5f);

	// ---- Bar --------------------------------------------------------------------------------

	private static void DrawBar(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, float opacity, float eased)
	{
		if (swatches.Count == 0)
			return;

		WheelDrawing.DrawRectOutline(spriteBatch, geometry.Strip, 2, RimColor * opacity);

		for (int i = 0; i < swatches.Count; i++)
			DrawBarCell(spriteBatch, config, settings, geometry, i, opacity);
	}

	private static void DrawBarCell(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, int index, float opacity)
	{
		int type = swatches[index];
		int total = index < swatchStacks.Count ? swatchStacks[index] : 0;
		bool hovered = index == hoveredSwatch && hoveredArrow < 0;

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
				settings.CellHeight * 0.55f, 1, SelectedColor * opacity);

		if (hovered) {
			WheelDrawing.DrawRectOutline(spriteBatch, cell, 1, RimColor * opacity);
			WheelDrawing.DrawRectOutline(spriteBatch, new Rectangle(cell.X - 1, cell.Y - 1, cell.Width + 2, cell.Height + 2),
				1, Color.White * opacity);
		}
	}

	// ---- Palette menu -----------------------------------------------------------------------

	private static void DrawMenu(SpriteBatch spriteBatch, in WheelLayout.Geometry geometry, float opacity)
	{
		if (menuRows.Count == 0)
			return;

		WheelLayout.MenuSettings menu = menuShape;
		Vector2 center = MenuCenter(geometry);
		Rectangle bounds = WheelLayout.MenuBounds(center, menu);

		WheelDrawing.DrawPanel(spriteBatch, bounds, opacity);
		DrawMenuTitle(spriteBatch, center, menu, opacity);

		for (int i = 0; i < menuRows.Count; i++) {
			MenuRow entry = menuRows[i];
			Rectangle row = WheelLayout.MenuRow(center, menu, i);
			bool hovered = i == hoveredRow;

			if (hovered)
				WheelDrawing.DrawRect(spriteBatch, row, MenuRowTint * (opacity * 0.9f));

			// A bar rather than an outline: an outline loses against the filled hover state.
			if (entry.Current)
				WheelDrawing.DrawRect(spriteBatch, new Rectangle(row.X, row.Y + 3, 3, row.Height - 6),
					SelectedColor * opacity);

			if (i < menuRows.Count - 1) {
				WheelDrawing.DrawRect(spriteBatch, new Rectangle(row.X + 4, row.Bottom - 1, row.Width - 8, 1),
					Color.Black * (opacity * 0.25f));
			}

			Color text = hovered ? Color.White : entry.Current ? SelectedColor : new Color(214, 216, 234);
			var at = new Vector2(row.X + RowLeftPad, row.Center.Y);

			string count = entry.Count > 0 ? entry.Count.ToString() : null;
			string label = WheelDrawing.Truncate(entry.Label, RowTextScale, NameRoom(row, entry));
			WheelDrawing.DrawTextLeft(spriteBatch, label, at, text, opacity, RowTextScale);

			if (count is not null) {
				float after = at.X + WheelDrawing.MeasureText(label, RowTextScale).X + RowCountGap;
				WheelDrawing.DrawTextLeft(spriteBatch, count, new Vector2(after, at.Y),
					new Color(150, 154, 184), opacity, RowCountScale);
			}

			if (entry.Preview is not null)
				DrawPalettePreview(spriteBatch, entry, row, opacity);
			else if (entry.Kind != RowKind.ScrapeTarget)
				DrawRowChevron(spriteBatch, row, hovered, opacity);

			DrawRowActions(spriteBatch, entry, row, hovered, opacity);
		}
	}

	/// <summary>
	/// The buttons that belong to a saved palette. They sit on the row itself rather than in a menu of
	/// their own, so acting on a palette does not mean first making it the one you are using.
	/// </summary>
	private static void DrawRowActions(SpriteBatch spriteBatch, MenuRow entry, Rectangle row,
		bool hoveredRowNow, float opacity)
	{
		int count = ActionCount(entry);
		if (count <= 0)
			return;

		for (int i = 0; i < count; i++) {
			Rectangle box = WheelLayout.MenuRowAction(row, i, count);
			bool lit = hoveredRowNow && hoveredAction == i;

			if (lit)
				WheelDrawing.DrawRect(spriteBatch, box, Color.White * (opacity * 0.16f));

			Color tint = (lit ? Color.White : new Color(168, 172, 200)) * opacity;
			Vector2 middle = box.Center.ToVector2();

			if (i == 0)
				WheelDrawing.DrawSliders(spriteBatch, middle, box.Width * 0.52f, tint);
			else
				WheelDrawing.DrawTrash(spriteBatch, middle, box.Width * 0.46f, lit ? EmptyMark * opacity : tint);
		}
	}

	private static void DrawMenuTitle(SpriteBatch spriteBatch, Vector2 center,
		in WheelLayout.MenuSettings menu, float opacity)
	{
		if (!menu.Titled)
			return;

		Rectangle band = WheelLayout.MenuTitle(center, menu);

		string title = Language.GetTextValue(drawOverlay == Overlay.Scrape
			? "Mods.PaintWheel.UI.Scrape.Header"
			: "Mods.PaintWheel.UI.PaletteTitle");

		// The band doubles as the readout for the buttons, which are too small to label themselves.
		if (drawOverlay == Overlay.Palettes) {
			if (hoveredPlus)
				title = Language.GetTextValue("Mods.PaintWheel.UI.NewPalette");
			else if (hoveredAction == 0)
				title = Language.GetTextValue("Mods.PaintWheel.UI.EditPalette");
			else if (hoveredAction == 1)
				title = Language.GetTextValue("Mods.PaintWheel.UI.DeletePalette");
		}

		WheelDrawing.DrawTextLeft(spriteBatch, title, new Vector2(band.X + 10f, band.Center.Y - 1f),
			Main.OurFavoriteColor, opacity, 0.78f);

		if (drawOverlay == Overlay.Palettes) {
			Rectangle add = WheelLayout.MenuTitleAction(center, menu);

			if (hoveredPlus)
				WheelDrawing.DrawRect(spriteBatch, add, Color.White * (opacity * 0.16f));

			WheelDrawing.DrawPlus(spriteBatch, add.Center.ToVector2(), add.Width * 0.5f,
				(hoveredPlus ? Color.White : new Color(168, 172, 200)) * opacity);
		}

		WheelDrawing.DrawRect(spriteBatch, new Rectangle(band.X + 4, band.Bottom - 1, band.Width - 8, 1),
			Color.Black * (opacity * 0.35f));
	}

	/// <summary>A small arrow on rows that lead somewhere rather than setting something.</summary>
	private static void DrawRowChevron(SpriteBatch spriteBatch, Rectangle row, bool hovered, float opacity)
	{
		var at = new Vector2(row.Right - 12f, row.Center.Y);

		WheelDrawing.DrawTriangle(spriteBatch, at + Vector2.One, 10f, 1, Color.Black * (opacity * 0.5f));
		WheelDrawing.DrawTriangle(spriteBatch, at, 10f, 1,
			(hovered ? Color.White : new Color(198, 202, 228)) * opacity);
	}

	/// <summary>A few dots of what a palette holds, so rows are told apart by colour, not just name.</summary>
	private static void DrawPalettePreview(SpriteBatch spriteBatch, MenuRow entry, Rectangle row, float opacity)
	{
		const int maxDots = RowPreviewDots;

		Source source = entry.Preview;
		int count = Math.Min(maxDots, source.Paints.Count);
		if (count <= 0)
			return;

		float size = row.Height * 0.42f;
		float step = size + 3f;

		// Stopping where the row's buttons begin, which is the room the width was measured to include.
		float right = row.Right - ActionRoom(entry) - 8f - size * 0.5f;

		for (int i = 0; i < count; i++) {
			// Spread the sample across the palette rather than showing only its first few colours.
			int pick = source.Paints.Count <= maxDots ? i : i * source.Paints.Count / maxDots;
			var at = new Vector2(right - (count - 1 - i) * step, row.Center.Y);

			WheelDrawing.DrawDisc(spriteBatch, at, size * 0.5f + 1f, RimColor * opacity);
			WheelDrawing.DrawDisc(spriteBatch, at, size * 0.5f - 0.5f, PaintCatalog.AccentColor(source.Paints[pick]) * opacity);
		}
	}

	/// <summary>
	/// The palette being built: every paint laid out at once, the way the config grid shows them, so
	/// choosing is a matter of looking rather than paging. Members carry the gold rim.
	/// </summary>
	private static void DrawGrid(SpriteBatch spriteBatch, in WheelLayout.Geometry geometry, float opacity)
	{
		Vector2 center = GridCenter(geometry);
		Rectangle bounds = WheelLayout.GridBounds(center, gridShape);

		WheelDrawing.DrawPanel(spriteBatch, bounds, opacity);
		DrawGridTitle(spriteBatch, center, opacity);

		Player player = Main.LocalPlayer;
		int position = 0;

		for (int i = 0; i < editPaints.Count; i++) {
			int type = editPaints[i];
			bool member = editMembers.Contains(type);
			bool hovered = i == hoveredCell;
			Rectangle cell = WheelLayout.GridCell(center, gridShape, i);
			Color accent = PaintCatalog.AccentColor(type);

			// A colour that is not in the palette is dimmed rather than merely unringed, so a full
			// palette does not read as a wall of gold with nothing to compare against.
			float strength = member ? 1f : hovered ? 0.72f : 0.42f;
			Color fill = Color.Lerp(new Color(38, 40, 58), accent, strength);

			WheelDrawing.DrawRect(spriteBatch, Inflate(cell, 1), Color.Black * (opacity * 0.45f));
			WheelDrawing.DrawRect(spriteBatch, cell, fill * opacity);

			if (member) {
				position++;

				WheelDrawing.DrawRectOutline(spriteBatch, cell, 2, ChosenRim * opacity);

				// White, always: DrawText already puts a black outline around every glyph, which is what
				// makes vanilla's stack counts readable on any background. Dark ink merges into it.
				WheelDrawing.DrawTextCentered(spriteBatch, position.ToString(),
					new Vector2(cell.Center.X, cell.Center.Y - 1f), Color.White, opacity, 0.7f);
			}
			else {
				WheelDrawing.DrawRectOutline(spriteBatch, cell, 1, CellRim * (opacity * 0.8f));
			}

			// Struck through when you are not carrying it: still worth adding, worth knowing you lack.
			if (PaintInventory.TotalStack(player, type) <= 0)
				WheelDrawing.DrawSlash(spriteBatch, cell.Center.ToVector2(), cell.Width * 0.66f, EmptyMark * opacity);

			if (hovered) {
				WheelDrawing.DrawRectOutline(spriteBatch, Inflate(cell, 2), 2, Color.White * opacity);
				WheelDrawing.DrawRectOutline(spriteBatch, cell, 1, Color.Black * (opacity * 0.5f));
			}
		}

		string hint = hoveredCell >= 0 && hoveredCell < editPaints.Count
			? Lang.GetItemNameValue(editPaints[hoveredCell])
			: Language.GetTextValue("Mods.PaintWheel.UI.EditHint");

		WheelDrawing.DrawTextCentered(spriteBatch, hint,
			new Vector2(bounds.Center.X, bounds.Bottom + 18f), Color.White, opacity, 0.85f);
	}

	private static Rectangle Inflate(Rectangle rect, int by)
		=> new(rect.X - by, rect.Y - by, rect.Width + by * 2, rect.Height + by * 2);


	private static void DrawGridTitle(SpriteBatch spriteBatch, Vector2 center, float opacity)
	{
		Rectangle band = WheelLayout.GridTitle(center, gridShape);

		// The palette being edited, which is not necessarily the one being used.
		string title = Language.GetTextValue("Mods.PaintWheel.UI.EditingHeader", editName ?? "");
		var at = new Vector2(band.X + 4f, band.Center.Y - 1f);

		WheelDrawing.DrawTextLeft(spriteBatch, title, at, Main.OurFavoriteColor, opacity, 0.8f);

		// Dimmed and set apart, so it reads as a tally rather than as part of the palette's name.
		WheelDrawing.DrawTextLeft(spriteBatch, editMembers.Count.ToString(),
			new Vector2(at.X + WheelDrawing.MeasureText(title, 0.8f).X + 9f, at.Y),
			new Color(150, 154, 184), opacity, 0.74f);

		Rectangle done = WheelLayout.GridTitleAction(center, gridShape);

		WheelDrawing.DrawRect(spriteBatch, done, (hoveredDone ? Color.White : RimColor) * (opacity * 0.22f));
		WheelDrawing.DrawRectOutline(spriteBatch, done, 1,
			(hoveredDone ? Color.White : new Color(120, 124, 156)) * opacity);

		WheelDrawing.DrawTextCentered(spriteBatch, Language.GetTextValue("Mods.PaintWheel.UI.DoneEditing"),
			new Vector2(done.Center.X, done.Center.Y - 1f),
			hoveredDone ? Color.White : new Color(214, 216, 234), opacity, 0.72f);

		WheelDrawing.DrawRect(spriteBatch, new Rectangle(band.X, band.Bottom - 1, band.Width, 1),
			RimColor * (opacity * 0.8f));
	}

	// ---- Shared -----------------------------------------------------------------------------

	private static void DrawCoatingRow(SpriteBatch spriteBatch, PaintWheelConfig config,
		in WheelLayout.Settings settings, in WheelLayout.Geometry geometry, float opacity, float eased)
	{
		Player player = Main.LocalPlayer;
		int chosen = PaintSelection.Coating;

		for (int i = 0; i < RowSlots; i++) {
			bool hovered = i == hoveredCoating;

			if (IsScrapeButton(i) || IsNoPaintButton(i)) {
				Vector2 buttonCenter = WheelLayout.CoatingCenter(settings, geometry, i);
				float buttonRadius = geometry.CoatingSize * 0.5f * eased * (hovered ? WheelLayout.HoverScale : 1f);

				if (IsScrapeButton(i))
					DrawScrapeButton(spriteBatch, buttonCenter, buttonRadius, hovered, opacity);
				else
					DrawNoPaintButton(spriteBatch, buttonCenter, buttonRadius, hovered, opacity);

				continue;
			}

			int type = coatingRow[i];
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
	/// What the gold ring means: normally the paint you are using, and while editing whether that
	/// colour is in the palette - which is the thing you are deciding then.
	/// </summary>
	private static bool Marked(int type)
		=> Editing ? editMembers.Contains(type) : type == PaintSelection.Paint;

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

		float y = hoveredSwatch >= 0 && hoveredSwatch < swatches.Count
			? WheelLayout.SwatchCenter(settings, geometry, hoveredSwatch).Y
			: geometry.InfoAnchor.Y;

		WheelDrawing.DrawTextLeft(spriteBatch, text, new Vector2(geometry.Strip.Right + 18f, y),
			Color.White, opacity, 0.9f);
	}

	private static string HoverText(PaintWheelConfig config)
	{
		if (hoveredCenter || hoveredHeader)
			return Language.GetTextValue("Mods.PaintWheel.UI.PaletteHint");

		if (hoveredArrow >= 0)
			return null;

		if (IsScrapeButton(hoveredCoating))
			return Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Enter");

		if (IsNoPaintButton(hoveredCoating)) {
			return Language.GetTextValue(PaintSelection.Paint == PaintSelection.NoPaint
				? "Mods.PaintWheel.UI.NoPaintResume"
				: "Mods.PaintWheel.UI.NoPaint");
		}

		if (hoveredCoating >= 0 && hoveredCoating < coatingRow.Count) {
			int type = coatingRow[hoveredCoating];
			return type <= 0
				? Language.GetTextValue("Mods.PaintWheel.UI.NoCoating")
				: Describe(type, PaintInventory.TotalStack(Main.LocalPlayer, type));
		}

		if (hoveredSwatch >= 0 && hoveredSwatch < swatches.Count) {
			int total = hoveredSwatch < swatchStacks.Count ? swatchStacks[hoveredSwatch] : 0;
			return Describe(swatches[hoveredSwatch], total);
		}

		// Holding a block with nothing to paint it: say so, rather than letting the pick quietly do
		// nothing when the block goes down.
		Player player = Main.LocalPlayer;
		if (!player.autoPaint && !PaintToolSet.IsPaintTool(player.HeldItem) && PaintToolSet.IsPlaceable(player.HeldItem))
			return Language.GetTextValue("Mods.PaintWheel.UI.NoAutoPaint");

		// Instructions only where they cost nothing. The wheel keeps a clear strip under the ring for
		// the hovered paint's name, so a hint sits in space that is already reserved; beside a bar or
		// under a grid it is one more thing on screen every time the picker opens.
		if (config.Layout != WheelLayoutStyle.Wheel)
			return null;

		if (PageCount(config) > 1)
			return Language.GetTextValue("Mods.PaintWheel.UI.ScrollHint");

		return sources.Count > 1 ? Language.GetTextValue("Mods.PaintWheel.UI.PaletteScrollHint") : null;
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
