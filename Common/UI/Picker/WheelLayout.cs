using System;
using Microsoft.Xna.Framework;
using PaintWheel.Common.Configs;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// Where every piece of the picker goes, for all three layouts, the lists and the editor grid. Pure -
/// no Main, no config instance, no drawing - so hit testing and drawing read the same numbers and
/// cannot disagree about where anything is.
/// </summary>
internal static class WheelLayout
{
	public const float ArrowSize = 26f;

	/// <summary>Clear space between the swatches and an arrow, so a near miss hits neither.</summary>
	public const float ArrowGap = 12f;

	/// <summary>Height of one row in a menu.</summary>
	public const float MenuRowHeight = 28f;

	/// <summary>Height of the band above the rows that holds the menu's title.</summary>
	public const float MenuTitleHeight = 21f;

	/// <summary>Padding around a menu's rows.</summary>
	public const float MenuPadding = 8f;

	/// <summary>Breathing room around the palette grid. Wider than the menu's: the grid is the page.</summary>
	public const float GridPadding = 13f;

	/// <summary>Taller than a menu title, so the button on it is not wedged against the colours.</summary>
	public const float GridTitleHeight = 28f;

	/// <summary>Bounds on menu width. Sized to its contents between these, so names are never clipped.</summary>
	public const float MenuMinWidth = 190f;

	public const float MenuMaxWidth = 330f;

	public const float HeaderGap = 24f;
	public const float InfoGap = 24f;
	public const float CoatingGap = 20f;

	/// <summary>Gap between the cursor and the left edge of the bar strip.</summary>
	public const float BarOffset = 26f;

	/// <summary>Room kept to the right of the strip for the hovered swatch's name.</summary>
	public const float BarLabelRoom = 170f;

	/// <summary>How far past the strip edges the cursor still counts as sweeping the bar.</summary>
	public const float BarGrabMargin = 24f;

	/// <summary>Extra scale applied to a hovered swatch, mirrored by the layout so it cannot clip.</summary>
	public const float HoverScale = 1.18f;

	/// <summary>How far outside a disc its gauge ring sits.</summary>
	public const float GaugeOffset = 5f;

	/// <summary>Size of one gauge pip for a given swatch size.</summary>
	public static float GaugePip(float swatch) => MathF.Max(2f, swatch * 0.075f);

	/// <summary>Room a swatch needs beyond its width, derived from the gauge so spacing cannot drift.</summary>
	public static float GaugeAllowance(float swatch) => GaugeOffset * 2f + GaugePip(swatch) + 1f;

	/// <summary>What the layout is being asked to fit: the shape, how many of everything, and the sizes.</summary>
	public struct Settings
	{
		public WheelLayoutStyle Style;
		public int Count;
		public int CoatingCount;
		public bool ShowHeader;

		/// <summary>More than one page, so the paging arrows are shown and can be clicked.</summary>
		public bool Paged;

		public float Radius;
		public float Swatch;
		public float DeadZone;

		public float CellWidth;
		public float CellHeight;

		/// <summary>Width of the header label, so the arrows can be placed clear of the text.</summary>
		public float LabelWidth;

		/// <summary>Eased open animation, 0 to 1.</summary>
		public float Progress;
	}

	/// <summary>Where everything ended up, for the hit tests and the drawing to share.</summary>
	public struct Geometry
	{
		public Vector2 Anchor;

		/// <summary>Full extents including header and coating row. Only used to stay on screen.</summary>
		public Rectangle Bounds;

		/// <summary>Square hugging the swatches, drawn only when the background panel is enabled.</summary>
		public Rectangle Panel;

		public Vector2 HeaderCenter;

		/// <summary>The header label's click box, which opens the palette menu.</summary>
		public Rectangle HeaderBox;

		public Rectangle LeftArrow;
		public Rectangle RightArrow;

		/// <summary>Centre of the swatches, and so of the palette menu that replaces them.</summary>
		public Vector2 MenuCenter;

		/// <summary>Where the hovered swatch's name goes. Centred for the wheel and the grid, left-aligned beside the bar.</summary>
		public Vector2 InfoAnchor;

		public float CoatingSize;
		public float CoatingCenterX;
		public float CoatingY;

		/// <summary>Bar only: the column of cells.</summary>
		public Rectangle Strip;

		/// <summary>Grid only: the shape of the rows of swatches.</summary>
		public GridSettings Cells;

		/// <summary>Wheel only: distance from the centre to the outer edge of a hovered swatch.</summary>
		public float Outer;

		/// <summary>Wheel only: the radius actually used, which may be widened past the configured one.</summary>
		public float Radius;
	}

	public static Geometry Compute(in Settings settings, Vector2 anchor) => settings.Style switch {
		WheelLayoutStyle.Bar => ComputeBar(settings, anchor),
		WheelLayoutStyle.Grid => ComputeGrid(settings, anchor),
		_ => ComputeWheel(settings, anchor),
	};

	// ---- Grid -------------------------------------------------------------------------------

	/// <summary>Columns in the grid layout, matching the config's palette grid and the editor.</summary>
	public const int GridColumns = 12;

	/// <summary>
	/// Rows of swatches under the cursor. The point of it is that nothing is hidden, so it is measured
	/// from the number of swatches rather than paged into a fixed shape.
	/// </summary>
	private static Geometry ComputeGrid(in Settings settings, Vector2 anchor)
	{
		var geometry = new Geometry { Anchor = anchor };

		geometry.Cells = new GridSettings {
			Count = Math.Max(1, settings.Count),

			// Narrower than twelve when there is less to show, so a short palette is a short grid
			// rather than one row rattling around in a full width panel.
			Columns = Math.Clamp(settings.Count, 1, GridColumns),
			Cell = MathF.Max(20f, settings.Swatch * 0.8f) * MathHelper.Lerp(0.85f, 1f, settings.Progress),
			Gap = 6f,
		};

		Rectangle bounds = GridBounds(anchor, geometry.Cells);

		geometry.Panel = bounds;
		geometry.MenuCenter = anchor;
		geometry.HeaderCenter = new Vector2(anchor.X, bounds.Top - HeaderGap * 0.6f);
		geometry.HeaderBox = HeaderHitBox(geometry.HeaderCenter, settings.LabelWidth);

		float spread = bounds.Width * 0.5f + ArrowGap + ArrowSize * 0.5f;
		geometry.LeftArrow = Box(new Vector2(anchor.X - spread, anchor.Y), ArrowSize);
		geometry.RightArrow = Box(new Vector2(anchor.X + spread, anchor.Y), ArrowSize);

		geometry.InfoAnchor = new Vector2(anchor.X, bounds.Bottom + InfoGap * 0.6f);

		geometry.CoatingSize = MathF.Max(24f, settings.Swatch * 0.72f);
		geometry.CoatingCenterX = anchor.X;
		geometry.CoatingY = geometry.InfoAnchor.Y + CoatingGap + geometry.CoatingSize * 0.5f;

		float halfWidth = MathF.Max(spread + ArrowSize * 0.5f + 2f, CoatingHalfWidth(settings, geometry));

		float top = settings.ShowHeader ? geometry.HeaderCenter.Y - 16f : bounds.Top;
		float bottom = settings.CoatingCount > 0
			? geometry.CoatingY + geometry.CoatingSize * 0.5f + 4f
			: geometry.InfoAnchor.Y + 16f;

		geometry.Bounds = FromEdges(anchor.X - halfWidth, top, anchor.X + halfWidth, bottom);

		return geometry;
	}

	// ---- Wheel ------------------------------------------------------------------------------

	/// <summary>
	/// The configured radius is a floor, not the answer: a ring wide enough for four swatches overlaps
	/// itself at twelve, so it grows to whatever keeps neighbours and their gauges apart.
	/// </summary>
	public static float EffectiveRadius(in Settings settings)
	{
		if (settings.Count < 3)
			return settings.Radius;

		float needed = settings.Swatch + GaugeAllowance(settings.Swatch);
		float minimum = needed / (2f * MathF.Sin(MathF.PI / settings.Count));

		return MathF.Max(settings.Radius, minimum);
	}

	private static Geometry ComputeWheel(in Settings settings, Vector2 anchor)
	{
		var geometry = new Geometry { Anchor = anchor };

		geometry.Radius = EffectiveRadius(settings);
		geometry.Outer = geometry.Radius + settings.Swatch * 0.5f * HoverScale;
		geometry.HeaderCenter = new Vector2(anchor.X, anchor.Y - geometry.Outer - HeaderGap);

		// Level with the centre, where the cursor already is, so paging is a flick sideways rather than
		// a trip up to the header. Outside the ring with a gap: overshooting a swatch must land on
		// nothing rather than on an arrow, which would take the release and cancel the pick.
		float spread = geometry.Outer + ArrowGap + ArrowSize * 0.5f;
		geometry.LeftArrow = Box(new Vector2(anchor.X - spread, anchor.Y), ArrowSize);
		geometry.RightArrow = Box(new Vector2(anchor.X + spread, anchor.Y), ArrowSize);
		geometry.HeaderBox = HeaderHitBox(geometry.HeaderCenter, settings.LabelWidth);
		geometry.MenuCenter = anchor;

		geometry.InfoAnchor = new Vector2(anchor.X, anchor.Y + geometry.Outer + InfoGap);

		geometry.CoatingSize = MathF.Max(24f, settings.Swatch * 0.72f);
		geometry.CoatingCenterX = anchor.X;
		geometry.CoatingY = geometry.InfoAnchor.Y + CoatingGap + geometry.CoatingSize * 0.5f;

		float pad = 14f;
		geometry.Panel = FromEdges(
			anchor.X - geometry.Outer - pad, anchor.Y - geometry.Outer - pad,
			anchor.X + geometry.Outer + pad, anchor.Y + geometry.Outer + pad);

		float halfWidth = MathF.Max(geometry.Outer + pad, spread + ArrowSize * 0.5f + 2f);
		halfWidth = MathF.Max(halfWidth, CoatingHalfWidth(settings, geometry));

		float top = settings.ShowHeader ? geometry.HeaderCenter.Y - 16f : anchor.Y - geometry.Outer;
		float bottom = settings.CoatingCount > 0
			? geometry.CoatingY + geometry.CoatingSize * 0.5f + 4f
			: geometry.InfoAnchor.Y + 16f;

		geometry.Bounds = FromEdges(anchor.X - halfWidth, top, anchor.X + halfWidth, bottom);

		return geometry;
	}

	// ---- Bar --------------------------------------------------------------------------------

	private static Geometry ComputeBar(in Settings settings, Vector2 anchor)
	{
		var geometry = new Geometry { Anchor = anchor };

		float width = settings.CellWidth;
		float height = settings.CellHeight * Math.Max(settings.Count, 1);

		// Slides out from the cursor rather than under it, so releasing without moving cancels.
		float left = anchor.X + BarOffset - (1f - settings.Progress) * 16f;
		float top = anchor.Y - height * 0.5f;

		geometry.Strip = FromEdges(left, top, left + width, top + height);
		geometry.Panel = FromEdges(left - 8f, top - 8f, left + width + 8f, top + height + 8f);

		float centerX = left + width * 0.5f;
		geometry.HeaderCenter = new Vector2(centerX, top - HeaderGap);

		// Beside the middle of the strip rather than above it, for the same reason as the wheel. The left
		// one is kept clear of the cursor, which the strip opens just beside: an arrow under the opening
		// position would be clicked by the first click of a picker left open.
		float spread = width * 0.5f + ArrowGap + ArrowSize * 0.5f;
		float middle = top + height * 0.5f;
		float leftX = settings.Paged ? MathF.Min(centerX - spread, anchor.X - ArrowGap - ArrowSize * 0.5f) : centerX - spread;
		geometry.LeftArrow = Box(new Vector2(leftX, middle), ArrowSize);
		geometry.RightArrow = Box(new Vector2(centerX + spread, middle), ArrowSize);
		geometry.HeaderBox = HeaderHitBox(geometry.HeaderCenter, settings.LabelWidth);
		geometry.MenuCenter = new Vector2(centerX, top + height * 0.5f);

		geometry.InfoAnchor = new Vector2(left + width + 12f, anchor.Y);

		geometry.CoatingSize = MathF.Max(24f, settings.CellHeight * 1.25f);
		geometry.CoatingCenterX = centerX;
		geometry.CoatingY = top + height + CoatingGap + geometry.CoatingSize * 0.5f;

		float boundsLeft = MathF.Min(anchor.X, geometry.LeftArrow.Left - 2f);
		boundsLeft = MathF.Min(boundsLeft, centerX - CoatingHalfWidth(settings, geometry));

		float boundsRight = MathF.Max(left + width + BarLabelRoom, centerX + CoatingHalfWidth(settings, geometry));
		boundsRight = MathF.Max(boundsRight, geometry.RightArrow.Right + 2f);

		float boundsTop = settings.ShowHeader ? geometry.HeaderCenter.Y - 16f : top;
		boundsTop = MathF.Min(boundsTop, geometry.LeftArrow.Top - 2f);

		float boundsBottom = settings.CoatingCount > 0
			? geometry.CoatingY + geometry.CoatingSize * 0.5f + 4f
			: top + height;

		boundsBottom = MathF.Max(boundsBottom, geometry.LeftArrow.Bottom + 2f);

		geometry.Bounds = FromEdges(boundsLeft, boundsTop, boundsRight, boundsBottom);

		return geometry;
	}

	// ---- Swatches ---------------------------------------------------------------------------

	/// <summary>Centre of swatch <paramref name="index"/>, matching <see cref="HitTestSwatch"/>.</summary>
	public static Vector2 SwatchCenter(in Settings settings, in Geometry geometry, int index)
	{
		if (settings.Style == WheelLayoutStyle.Grid) {
			Rectangle cell = GridCell(geometry.Anchor, geometry.Cells, index);

			return new Vector2(cell.X + cell.Width * 0.5f, cell.Y + cell.Height * 0.5f);
		}

		if (settings.Style == WheelLayoutStyle.Bar) {
			return new Vector2(
				geometry.Strip.X + geometry.Strip.Width * 0.5f,
				geometry.Strip.Y + (index + 0.5f) * settings.CellHeight);
		}

		return WheelMath.SectorPosition(geometry.Anchor, index, settings.Count, geometry.Radius * settings.Progress);
	}

	/// <summary>
	/// Which swatch the cursor is choosing, or -1. The wheel picks by angle so a flick is enough; the
	/// bar picks by row, with a generous margin so a sweep need not be accurate sideways.
	/// </summary>
	public static int HitTestSwatch(in Settings settings, in Geometry geometry, Vector2 cursor)
	{
		if (settings.Count <= 0)
			return -1;

		if (settings.Style == WheelLayoutStyle.Grid)
			return HitTestGrid(geometry.Anchor, geometry.Cells, cursor);

		if (settings.Style != WheelLayoutStyle.Bar)
			return WheelMath.SectorAt(geometry.Anchor, cursor, settings.Count, settings.DeadZone);

		Rectangle strip = geometry.Strip;
		if (cursor.X < strip.Left - BarGrabMargin || cursor.X > strip.Right + BarLabelRoom)
			return -1;

		if (cursor.Y < strip.Top || cursor.Y >= strip.Bottom)
			return -1;

		int index = (int)((cursor.Y - strip.Top) / settings.CellHeight);
		return Math.Clamp(index, 0, settings.Count - 1);
	}

	// ---- Coating row ------------------------------------------------------------------------

	public static Vector2 CoatingCenter(in Settings settings, in Geometry geometry, int index)
	{
		float spacing = geometry.CoatingSize + 8f;
		float width = (settings.CoatingCount - 1) * spacing;
		return new Vector2(geometry.CoatingCenterX - width * 0.5f + index * spacing, geometry.CoatingY);
	}

	public static int HitTestCoating(in Settings settings, in Geometry geometry, Vector2 cursor)
	{
		float half = geometry.CoatingSize * 0.5f;

		for (int i = 0; i < settings.CoatingCount; i++) {
			Vector2 center = CoatingCenter(settings, geometry, i);
			if (cursor.X >= center.X - half && cursor.X <= center.X + half
				&& cursor.Y >= center.Y - half && cursor.Y <= center.Y + half)
				return i;
		}

		return -1;
	}

	private static float CoatingHalfWidth(in Settings settings, in Geometry geometry)
	{
		if (settings.CoatingCount <= 0)
			return 0f;

		float spacing = geometry.CoatingSize + 8f;
		return (settings.CoatingCount - 1) * spacing * 0.5f + geometry.CoatingSize * 0.5f + 4f;
	}

	// ---- Menus ------------------------------------------------------------------------------

	/// <summary>
	/// A vertical list, not another ring: its entries are words. Width is passed in because only the
	/// caller can measure its own text, and a fixed width either clips long names or wastes space.
	/// </summary>
	public struct MenuSettings
	{
		public int Rows;

		/// <summary>Requested width. Clamped to <see cref="MenuMinWidth"/>..<see cref="MenuMaxWidth"/>.</summary>
		public float Width;

		/// <summary>True when a title band is drawn above the rows.</summary>
		public bool Titled;

		public float ClampedWidth => MathHelper.Clamp(Width, MenuMinWidth, MenuMaxWidth);

		public float TitleBand => Titled ? MenuTitleHeight : 0f;
	}

	public static Rectangle MenuBounds(Vector2 center, in MenuSettings menu)
	{
		float width = menu.ClampedWidth;
		float height = Math.Max(1, menu.Rows) * MenuRowHeight + menu.TitleBand + MenuPadding * 2f;

		return FromEdges(
			center.X - width * 0.5f, center.Y - height * 0.5f,
			center.X + width * 0.5f, center.Y + height * 0.5f);
	}

	public static Rectangle MenuRow(Vector2 center, in MenuSettings menu, int index)
	{
		Rectangle bounds = MenuBounds(center, menu);
		float top = bounds.Top + MenuPadding + menu.TitleBand;

		return FromEdges(
			bounds.Left + MenuPadding, top + index * MenuRowHeight,
			bounds.Right - MenuPadding, top + (index + 1) * MenuRowHeight);
	}

	/// <summary>The title band, which is never part of a row and so never selects anything.</summary>
	public static Rectangle MenuTitle(Vector2 center, in MenuSettings menu)
	{
		Rectangle bounds = MenuBounds(center, menu);

		return FromEdges(bounds.Left + MenuPadding, bounds.Top + MenuPadding,
			bounds.Right - MenuPadding, bounds.Top + MenuPadding + menu.TitleBand);
	}

	/// <summary>Width one row button needs, including the gap before the next.</summary>
	public const float MenuActionSize = 19f;

	public const float MenuActionGap = 3f;

	/// <summary>Room a row must leave on its right for <paramref name="count"/> buttons.</summary>
	public static float MenuActionRoom(int count)
		=> count <= 0 ? 0f : count * (MenuActionSize + MenuActionGap) + MenuActionGap;

	/// <summary>
	/// One of the buttons at the right of a row, numbered from the left of the group so the order
	/// matches the order they are drawn and described in.
	/// </summary>
	public static Rectangle MenuRowAction(Rectangle row, int index, int count)
	{
		float right = row.Right - MenuActionGap;
		float left = right - MenuActionRoom(count) + MenuActionGap + index * (MenuActionSize + MenuActionGap);
		float top = row.Y + (row.Height - MenuActionSize) * 0.5f;

		return FromEdges(left, top, left + MenuActionSize, top + MenuActionSize);
	}

	/// <summary>The button at the right of the title band, for adding a palette.</summary>
	public static Rectangle MenuTitleAction(Vector2 center, in MenuSettings menu)
	{
		Rectangle band = MenuTitle(center, menu);
		float top = band.Y + (band.Height - MenuActionSize) * 0.5f;

		return FromEdges(band.Right - MenuActionSize, top, band.Right, top + MenuActionSize);
	}

	public static int HitTestMenu(Vector2 center, in MenuSettings menu, Vector2 cursor)
	{
		for (int i = 0; i < menu.Rows; i++) {
			if (MenuRow(center, menu, i).Contains((int)cursor.X, (int)cursor.Y))
				return i;
		}

		return -1;
	}

	/// <summary>Keeps a tall menu on screen even when the picker was opened near an edge.</summary>
	public static Vector2 ClampMenuCenter(Vector2 center, in MenuSettings menu, float screenWidth, float screenHeight)
		=> ClampOnScreen(center, MenuBounds(center, menu), screenWidth, screenHeight);

	/// <summary>
	/// Moves a centred box just far enough to be fully on screen, or centres it when it is larger than
	/// the screen in that direction.
	/// </summary>
	private static Vector2 ClampOnScreen(Vector2 center, Rectangle bounds, float screenWidth, float screenHeight)
	{
		float halfWidth = bounds.Width * 0.5f;
		float halfHeight = bounds.Height * 0.5f;

		float x = screenWidth > bounds.Width
			? MathHelper.Clamp(center.X, halfWidth + 4f, screenWidth - halfWidth - 4f)
			: screenWidth * 0.5f;

		float y = screenHeight > bounds.Height
			? MathHelper.Clamp(center.Y, halfHeight + 4f, screenHeight - halfHeight - 4f)
			: screenHeight * 0.5f;

		return new Vector2(x, y);
	}

	// ---- Palette grid -----------------------------------------------------------------------

	/// <summary>
	/// Every paint at once, in rows. Building a palette means comparing colours against each other and
	/// reaching for one you can already see - a ring you have to page through is the wrong shape for
	/// that, however well it suits picking one colour in a hurry.
	/// </summary>
	public struct GridSettings
	{
		public int Count;
		public int Columns;
		public float Cell;
		public float Gap;

		/// <summary>True when a title band is kept above the colours, as the editor does.</summary>
		public bool Titled;

		public readonly float TitleBand => Titled ? GridTitleHeight + GridTitleGap : 0f;

		public readonly int Rows => Math.Max(1, (Count + Math.Max(1, Columns) - 1) / Math.Max(1, Columns));

		public readonly float Step => Cell + Gap;

		public readonly float Width => Math.Max(1, Columns) * Step - Gap;

		public readonly float Height => Rows * Step - Gap;
	}

	public static Rectangle GridBounds(Vector2 center, in GridSettings grid)
	{
		float width = grid.Width + GridPadding * 2f;
		float height = grid.Height + GridPadding * 2f + grid.TitleBand;

		return FromEdges(
			center.X - width * 0.5f, center.Y - height * 0.5f,
			center.X + width * 0.5f, center.Y + height * 0.5f);
	}

	/// <summary>The title band above the colours, which never selects one.</summary>
	public static Rectangle GridTitle(Vector2 center, in GridSettings grid)
	{
		Rectangle bounds = GridBounds(center, grid);

		return FromEdges(bounds.Left + GridPadding, bounds.Top + GridPadding,
			bounds.Right - GridPadding, bounds.Top + GridPadding + GridTitleHeight);
	}

	/// <summary>Gap between the title band's rule and the first row of colours.</summary>
	public const float GridTitleGap = 7f;

	/// <summary>Width of the button that finishes editing. Wide enough for the word in any language.</summary>
	public const float GridActionWidth = 70f;

	public static Rectangle GridTitleAction(Vector2 center, in GridSettings grid)
	{
		Rectangle band = GridTitle(center, grid);
		float height = MenuActionSize + 3f;
		float top = band.Y + (band.Height - height) * 0.5f;

		return FromEdges(band.Right - GridActionWidth, top, band.Right, top + height);
	}

	public static Rectangle GridCell(Vector2 center, in GridSettings grid, int index)
	{
		Rectangle bounds = GridBounds(center, grid);
		int columns = Math.Max(1, grid.Columns);

		float left = bounds.Left + GridPadding + index % columns * grid.Step;
		float top = bounds.Top + GridPadding + grid.TitleBand + index / columns * grid.Step;

		return FromEdges(left, top, left + grid.Cell, top + grid.Cell);
	}

	public static int HitTestGrid(Vector2 center, in GridSettings grid, Vector2 cursor)
	{
		for (int i = 0; i < grid.Count; i++) {
			if (GridCell(center, grid, i).Contains((int)cursor.X, (int)cursor.Y))
				return i;
		}

		return -1;
	}

	public static Vector2 ClampGridCenter(Vector2 center, in GridSettings grid, float screenWidth, float screenHeight)
		=> ClampOnScreen(center, GridBounds(center, grid), screenWidth, screenHeight);

	// ---- Placement --------------------------------------------------------------------------

	/// <summary>Nudges the anchor so the whole picker stays on screen when opened near an edge.</summary>
	public static Vector2 ClampAnchor(in Settings settings, Vector2 desired, float screenWidth, float screenHeight)
	{
		Geometry geometry = Compute(settings, desired);
		Rectangle bounds = geometry.Bounds;

		float left = desired.X - bounds.Left;
		float right = bounds.Right - desired.X;
		float up = desired.Y - bounds.Top;
		float down = bounds.Bottom - desired.Y;

		float x = screenWidth > left + right
			? MathHelper.Clamp(desired.X, left, screenWidth - right)
			: screenWidth * 0.5f;

		float y = screenHeight > up + down
			? MathHelper.Clamp(desired.Y, up, screenHeight - down)
			: screenHeight * 0.5f;

		return new Vector2(x, y);
	}

	/// <summary>
	/// Half the header's click box, covering the text and the marker past it. The arrow spread derives
	/// from this so the two can never overlap as click targets.
	/// </summary>
	private static float HeaderHalfWidth(float labelWidth) => MathF.Max(44f, labelWidth * 0.5f + 16f);

	private static Rectangle HeaderHitBox(Vector2 center, float labelWidth)
	{
		float halfWidth = HeaderHalfWidth(labelWidth);

		return FromEdges(center.X - halfWidth, center.Y - 12f, center.X + halfWidth, center.Y + 12f);
	}

	private static Rectangle Box(Vector2 center, float size)
		=> FromEdges(center.X - size * 0.5f, center.Y - size * 0.5f, center.X + size * 0.5f, center.Y + size * 0.5f);

	private static Rectangle FromEdges(float left, float top, float right, float bottom)
		=> new((int)MathF.Round(left), (int)MathF.Round(top),
			(int)MathF.Round(right - left), (int)MathF.Round(bottom - top));
}
