using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PaintWheel.Common.UI;

/// <summary>
/// Pixel-art drawing helpers, in UI space - InterfaceScaleType.UI applies the matching matrix, so one
/// set of pixel sizes fits every UI scale.
/// <para/>
/// Circles are filled scanline by scanline from MagicPixel, not scaled from a circle texture: that
/// keeps the edge a hard pixel staircase at any size, which is what sits right next to Terraria's art.
/// </summary>
public static class WheelDrawing
{
	/// <summary>The standard translucent panel tint Terraria uses behind inventory UI.</summary>
	public static readonly Color PanelTint = new Color(63, 65, 151) * 0.785f;

	private static readonly Dictionary<int, int[]> discSpans = new();

	public static void ClearCaches() => discSpans.Clear();

	public static void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float opacity)
		=> Utils.DrawInvBG(spriteBatch, rect, PanelTint * opacity);

	// ---- Primitives -------------------------------------------------------------------------

	/// <summary>A filled pixel circle. Half-widths per row are cached, so this is a handful of quads.</summary>
	public static void DrawDisc(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
	{
		int r = (int)MathF.Round(radius);
		if (r < 1 || color.A == 0)
			return;

		int[] spans = SpansFor(r);
		int cx = (int)MathF.Round(center.X);
		int cy = (int)MathF.Round(center.Y);
		Texture2D pixel = TextureAssets.MagicPixel.Value;

		for (int y = -r; y <= r; y++) {
			int half = spans[Math.Abs(y)];
			if (half > 0)
				spriteBatch.Draw(pixel, new Rectangle(cx - half, cy + y, half * 2, 1), color);
		}
	}

	/// <summary>A circular outline of the given thickness, drawn as two discs.</summary>
	public static void DrawDiscOutline(SpriteBatch spriteBatch, Vector2 center, float radius, float thickness,
		Color color)
	{
		int outer = (int)MathF.Round(radius);
		int inner = Math.Max(0, outer - Math.Max(1, (int)MathF.Round(thickness)));
		if (outer < 1 || color.A == 0)
			return;

		int[] outerSpans = SpansFor(outer);
		int[] innerSpans = inner > 0 ? SpansFor(inner) : null;
		int cx = (int)MathF.Round(center.X);
		int cy = (int)MathF.Round(center.Y);
		Texture2D pixel = TextureAssets.MagicPixel.Value;

		for (int y = -outer; y <= outer; y++) {
			int half = outerSpans[Math.Abs(y)];
			if (half <= 0)
				continue;

			int hole = innerSpans is not null && Math.Abs(y) <= inner ? innerSpans[Math.Abs(y)] : 0;
			if (hole <= 0) {
				spriteBatch.Draw(pixel, new Rectangle(cx - half, cy + y, half * 2, 1), color);
				continue;
			}

			spriteBatch.Draw(pixel, new Rectangle(cx - half, cy + y, half - hole, 1), color);
			spriteBatch.Draw(pixel, new Rectangle(cx + hole, cy + y, half - hole, 1), color);
		}
	}

	/// <summary>An axis-aligned rectangle of solid colour.</summary>
	public static void DrawRect(SpriteBatch spriteBatch, Rectangle rect, Color color)
	{
		if (rect.Width > 0 && rect.Height > 0 && color.A != 0)
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, color);
	}

	/// <summary>A one-pixel border just outside the given rectangle.</summary>
	public static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, int thickness, Color color)
	{
		if (thickness <= 0 || color.A == 0)
			return;

		DrawRect(spriteBatch, new Rectangle(rect.X - thickness, rect.Y - thickness, rect.Width + thickness * 2, thickness), color);
		DrawRect(spriteBatch, new Rectangle(rect.X - thickness, rect.Bottom, rect.Width + thickness * 2, thickness), color);
		DrawRect(spriteBatch, new Rectangle(rect.X - thickness, rect.Y, thickness, rect.Height), color);
		DrawRect(spriteBatch, new Rectangle(rect.Right, rect.Y, thickness, rect.Height), color);
	}

	/// <summary>An axis-aligned square of solid colour, centred on a point. Never rotated.</summary>
	public static void DrawPixelSquare(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
	{
		spriteBatch.Draw(TextureAssets.MagicPixel.Value, center, new Rectangle(0, 0, 1, 1), color, 0f,
			new Vector2(0.5f), size, SpriteEffects.None, 0f);
	}

	/// <summary>A solid pixel triangle for the paging arrows. <paramref name="direction"/> is -1 or 1.</summary>
	public static void DrawTriangle(SpriteBatch spriteBatch, Vector2 center, float size, int direction, Color color)
	{
		int half = Math.Max(2, (int)MathF.Round(size * 0.5f));
		int cx = (int)MathF.Round(center.X);
		int cy = (int)MathF.Round(center.Y);
		Texture2D pixel = TextureAssets.MagicPixel.Value;

		for (int step = 0; step < half; step++) {
			int x = direction < 0 ? cx - half + step : cx + half - step - 1;
			int height = step * 2 + 1;
			spriteBatch.Draw(pixel, new Rectangle(x, cy - step, 1, height), color);
		}
	}

	/// <summary>
	/// A solid pixel triangle pointing up or down. <paramref name="direction"/> is -1 for up, 1 for down.
	/// </summary>
	public static void DrawTriangleVertical(SpriteBatch spriteBatch, Vector2 center, float size, int direction,
		Color color)
	{
		int half = Math.Max(2, (int)MathF.Round(size * 0.5f));
		int cx = (int)MathF.Round(center.X);
		int cy = (int)MathF.Round(center.Y);
		Texture2D pixel = TextureAssets.MagicPixel.Value;

		for (int step = 0; step < half; step++) {
			int y = direction < 0 ? cy - half + step : cy + half - step - 1;
			int width = step * 2 + 1;
			spriteBatch.Draw(pixel, new Rectangle(cx - step, y, width, 1), color);
		}
	}

	/// <summary>A diagonal bar of pixel blocks, marking a swatch the player has none of.</summary>
	public static void DrawSlash(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
	{
		float step = size / 4f;
		float pip = MathF.Max(2f, size / 4.5f);

		for (int i = -2; i <= 2; i++)
			DrawPixelSquare(spriteBatch, center + new Vector2(i * step, -i * step), pip, color);
	}

	/// <summary>
	/// Three sliders, for the button that opens a palette for editing. Drawn rather than typed: the UI
	/// font has no gear, and a missing glyph renders as a box.
	/// </summary>
	public static void DrawSliders(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
	{
		float half = size * 0.5f;
		float thickness = MathF.Max(1f, size * 0.09f);
		float knob = MathF.Max(2f, size * 0.22f);

		for (int i = -1; i <= 1; i++) {
			float y = center.Y + i * size * 0.3f;

			DrawRect(spriteBatch, new Rectangle(
				(int)(center.X - half), (int)(y - thickness * 0.5f),
				(int)size, (int)MathF.Max(1f, thickness)), color);

			// Knobs staggered, so it reads as settings rather than as a list.
			DrawPixelSquare(spriteBatch, new Vector2(center.X + i * size * 0.24f, y), knob, color);
		}
	}

	/// <summary>A bin, for the button that removes a palette.</summary>
	public static void DrawTrash(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
	{
		float half = size * 0.5f;
		float lid = MathF.Max(1f, size * 0.12f);

		DrawRect(spriteBatch, new Rectangle(
			(int)(center.X - half), (int)(center.Y - half), (int)size, (int)lid), color);

		DrawRect(spriteBatch, new Rectangle(
			(int)(center.X - size * 0.16f), (int)(center.Y - half - lid),
			(int)(size * 0.32f), (int)lid), color);

		var body = new Rectangle(
			(int)(center.X - size * 0.36f), (int)(center.Y - half + lid + 1f),
			(int)(size * 0.72f), (int)(size * 0.78f));

		DrawRectOutline(spriteBatch, body, (int)MathF.Max(1f, lid), color);

		for (int i = -1; i <= 1; i += 2) {
			DrawRect(spriteBatch, new Rectangle(
				(int)(center.X + i * size * 0.14f), body.Y + 3, (int)MathF.Max(1f, lid), body.Height - 6), color);
		}
	}

	/// <summary>A plus, for the button that starts a new palette.</summary>
	public static void DrawPlus(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
	{
		float thickness = MathF.Max(2f, size * 0.2f);

		DrawRect(spriteBatch, new Rectangle(
			(int)(center.X - size * 0.5f), (int)(center.Y - thickness * 0.5f), (int)size, (int)thickness), color);

		DrawRect(spriteBatch, new Rectangle(
			(int)(center.X - thickness * 0.5f), (int)(center.Y - size * 0.5f), (int)thickness, (int)size), color);
	}

	/// <summary>A small X built from pixel blocks.</summary>
	public static void DrawCross(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
	{
		float step = size / 4f;
		float pip = MathF.Max(2f, size / 5f);

		for (int i = -2; i <= 2; i++) {
			DrawPixelSquare(spriteBatch, center + new Vector2(i * step, i * step), pip, color);
			if (i != 0)
				DrawPixelSquare(spriteBatch, center + new Vector2(i * step, -i * step), pip, color);
		}
	}

	// ---- Content ----------------------------------------------------------------------------

	/// <summary>Draws the real item sprite for a paint, sized the way vanilla sizes inventory icons.</summary>
	public static void DrawItemIcon(SpriteBatch spriteBatch, int itemType, Vector2 center, float slotScale, Color color)
	{
		if (itemType <= 0)
			return;

		Main.instance.LoadItem(itemType);

		var asset = TextureAssets.Item[itemType];
		if (asset is null || !asset.IsLoaded)
			return;

		Texture2D texture = asset.Value;
		Rectangle frame = Item.GetDrawHitbox(itemType, null);
		if (frame.Width <= 0 || frame.Height <= 0)
			return;

		// Same rule vanilla uses for inventory slots: shrink oversized sprites to fit, never upscale.
		float fit = 1f;
		int longest = Math.Max(frame.Width, frame.Height);
		if (longest > 32)
			fit = 32f / longest;

		Vector2 origin = new(frame.Width / 2f, frame.Height / 2f);
		spriteBatch.Draw(texture, center, frame, color, 0f, origin, fit * slotScale, SpriteEffects.None, 0f);
	}

	/// <summary>
	/// A texture centred on a point and fitted to <paramref name="size"/> pixels across. Nothing when the
	/// texture has not loaded, so it is safe to call before it has.
	/// </summary>
	public static void DrawIcon(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, float size, Color color)
	{
		if (texture is null || size <= 0f || color.A == 0)
			return;

		float scale = size / Math.Max(texture.Width, texture.Height);
		spriteBatch.Draw(texture, center, null, color, 0f, new Vector2(texture.Width, texture.Height) * 0.5f, scale,
			SpriteEffects.None, 0f);
	}

	/// <summary>A segmented supply ring. Discrete pips, because an anti-aliased arc reads as foreign.</summary>
	public static void DrawSupplyRing(SpriteBatch spriteBatch, Vector2 center, float radius, int segments,
		float fill, float pipSize, Color lit, Color unlit)
	{
		if (segments <= 0)
			return;

		fill = MathHelper.Clamp(fill, 0f, 1f);
		int on = (int)Math.Round(fill * segments);
		if (fill > 0f && on == 0)
			on = 1;

		for (int i = 0; i < segments; i++) {
			float angle = i * MathHelper.TwoPi / segments;
			Vector2 offset = new(MathF.Sin(angle) * radius, -MathF.Cos(angle) * radius);
			DrawPixelSquare(spriteBatch, center + offset, pipSize, i < on ? lit : unlit);
		}
	}

	/// <summary>A horizontal supply bar, the bar and grid layouts' equivalent of the depletion ring.</summary>
	public static void DrawSupplyBar(SpriteBatch spriteBatch, Rectangle rect, int segments, float fill,
		Color lit, Color unlit)
	{
		if (segments <= 0 || rect.Width <= 0)
			return;

		fill = MathHelper.Clamp(fill, 0f, 1f);
		int on = (int)Math.Round(fill * segments);
		if (fill > 0f && on == 0)
			on = 1;

		float step = rect.Width / (float)segments;
		int pip = Math.Max(1, (int)MathF.Round(step) - 1);

		for (int i = 0; i < segments; i++) {
			var cell = new Rectangle(rect.X + (int)MathF.Round(i * step), rect.Y, pip, rect.Height);
			DrawRect(spriteBatch, cell, i < on ? lit : unlit);
		}
	}

	// ---- Text -------------------------------------------------------------------------------

	public static Vector2 MeasureText(string text, float scale = 1f)
		=> string.IsNullOrEmpty(text) ? Vector2.Zero : FontAssets.MouseText.Value.MeasureString(text) * scale;

	/// <summary>
	/// Trims text to fit, ending in dots when it had to cut. Nothing here clips text, so an over-long
	/// name would otherwise draw straight over its neighbours.
	/// </summary>
	public static string Truncate(string text, float scale, float maxWidth)
		=> Truncate(text, candidate => MeasureText(candidate, scale).X, maxWidth);

	/// <summary>
	/// The trimming itself, with measuring injected: font metrics need a loaded font, and this loop is
	/// the half worth testing.
	/// </summary>
	public static string Truncate(string text, Func<string, float> measure, float maxWidth)
	{
		if (string.IsNullOrEmpty(text) || maxWidth <= 0f || measure(text) <= maxWidth)
			return text;

		for (int length = text.Length - 1; length > 0; length--) {
			string trimmed = text[..length].TrimEnd() + "..";

			if (measure(trimmed) <= maxWidth)
				return trimmed;
		}

		return "..";
	}

	/// <summary>
	/// A small number in vanilla's stack-count font, outlined the same way - for the key hints and
	/// stack counts on swatches, which read like the hotbar's own.
	/// </summary>
	public static void DrawSmallNumber(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale)
	{
		if (string.IsNullOrEmpty(text) || color.A == 0)
			return;

		Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, text, position.X, position.Y,
			color, Color.Black * (color.A / 255f), Vector2.Zero, scale);
	}

	/// <summary>
	/// One colour cell, drawn the same wherever paints are laid out in a grid - the Grid layout, the
	/// in-game editor and the config's palette block - so the three read as one control: a dark backing,
	/// the colour, a gold rim when it is the chosen one (or in the palette), a number in the corner in
	/// the stack-count font, and a white ring outside it all on hover.
	/// </summary>
	public static void DrawPaletteCell(SpriteBatch spriteBatch, Rectangle cell, Color fill, bool marked,
		bool hovered, string corner, float opacity)
	{
		DrawRect(spriteBatch, new Rectangle(cell.X - 1, cell.Y - 1, cell.Width + 2, cell.Height + 2), Color.Black * (opacity * 0.45f));
		DrawRect(spriteBatch, cell, fill * opacity);

		if (marked)
			DrawRectOutline(spriteBatch, cell, 2, UIColors.Chosen * opacity);
		else
			DrawRectOutline(spriteBatch, cell, 1, UIColors.CellRim * (opacity * 0.8f));

		DrawCellCorner(spriteBatch, cell, corner, opacity);

		if (hovered) {
			DrawRectOutline(spriteBatch, new Rectangle(cell.X - 2, cell.Y - 2, cell.Width + 4, cell.Height + 4), 2, Color.White * opacity);
			DrawRectOutline(spriteBatch, cell, 1, Color.Black * (opacity * 0.5f));
		}
	}

	/// <summary>A number tucked into a cell's bottom-right corner, where vanilla puts a stack count.</summary>
	public static void DrawCellCorner(SpriteBatch spriteBatch, Rectangle cell, string text, float opacity)
	{
		if (string.IsNullOrEmpty(text))
			return;

		const float scale = 0.62f;
		Vector2 size = MeasureSmallNumber(text, scale);
		DrawSmallNumber(spriteBatch, text, new Vector2(cell.Right - size.X - 2f, cell.Bottom - size.Y - 1f), Color.White * opacity, scale);
	}

	/// <summary>Size of <see cref="DrawSmallNumber"/>'s text.</summary>
	public static Vector2 MeasureSmallNumber(string text, float scale)
		=> string.IsNullOrEmpty(text) ? Vector2.Zero : FontAssets.ItemStack.Value.MeasureString(text) * scale;

	/// <summary>Text with the four-way black outline vanilla uses for every readable UI string.</summary>
	public static void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color,
		float opacity, float scale = 1f)
	{
		if (string.IsNullOrEmpty(text))
			return;

		Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, position.X, position.Y,
			color * opacity, Color.Black * (opacity * 0.8f), Vector2.Zero, scale);
	}

	public static void DrawTextCentered(SpriteBatch spriteBatch, string text, Vector2 center, Color color,
		float opacity, float scale = 1f)
		=> DrawText(spriteBatch, text, center - MeasureText(text, scale) / 2f, color, opacity, scale);

	/// <summary>Left-aligned but vertically centred, for labels beside a bar cell.</summary>
	public static void DrawTextLeft(SpriteBatch spriteBatch, string text, Vector2 anchor, Color color,
		float opacity, float scale = 1f)
		=> DrawText(spriteBatch, text, new Vector2(anchor.X, anchor.Y - MeasureText(text, scale).Y / 2f),
			color, opacity, scale);

	// ---- Internals --------------------------------------------------------------------------

	private static int[] SpansFor(int radius)
	{
		if (discSpans.TryGetValue(radius, out int[] cached))
			return cached;

		var spans = new int[radius + 1];
		for (int y = 0; y <= radius; y++)
			spans[y] = (int)Math.Round(Math.Sqrt(radius * radius - y * y));

		discSpans[radius] = spans;
		return spans;
	}
}
