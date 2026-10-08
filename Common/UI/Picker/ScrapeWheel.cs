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
/// Scrape mode's own small wheel, from the colour wheel's art: a ring of the bottom row's buttons for
/// what the scraper may strip, showing a wood block and a wood wall, round the painter's palette in
/// the middle that goes back to the colours, under its title on a name rack. It stands in for the
/// swatches while scrape mode is on, whatever the picker's shape. Geometry is
/// <see cref="WheelLayout.ComputeScrape"/>'s; clicks and releases are <see cref="PickerInput"/>'s and
/// <see cref="PaintPicker"/>'s, as for everything else.
/// </summary>
internal static class ScrapeWheel
{
	/// <summary>Round the ring clockwise from the top: the block, the wall, both, then coatings alone.</summary>
	internal static readonly ScrapeMode[] Options = {
		ScrapeMode.BlocksOnly,
		ScrapeMode.WallsOnly,
		ScrapeMode.BlocksAndWalls,
		ScrapeMode.CoatingsOnly,
	};

	/// <summary>
	/// Where the wheel sits: where the cursor was when it appeared, kept on screen. Not the picker's
	/// anchor - reached from the scraper button under the swatches, a flick has to start from where the
	/// cursor already is.
	/// </summary>
	internal static Vector2 Center;

	/// <summary>The colour wheel in miniature, for the way back to it: a hue from each part of the ring.</summary>
	private static readonly int[] BackColours = {
		ItemID.RedPaint, ItemID.YellowPaint, ItemID.GreenPaint, ItemID.CyanPaint, ItemID.BluePaint, ItemID.PurplePaint,
	};

	// Flat stand-ins for the wood images, for the frames before they have loaded.
	private static readonly Color BlockFallback = new(151, 107, 75);
	private static readonly Color WallFallback = new(69, 50, 37);

	/// <summary>
	/// Cooler and lighter than the scraper button's brown, so both woods read against it - the wall is
	/// the darker of the two, as walls always are, and vanishes into anything wood coloured.
	/// </summary>
	private static readonly Color OptionFill = new(92, 98, 132);

	private static readonly Color CoatEdge = new(222, 238, 255);

	internal static WheelLayout.ScrapeSettings Shape(in WheelLayout.Settings settings)
		=> WheelLayout.ComputeScrape(settings, Options.Length);

	/// <summary>Centres the wheel on <paramref name="at"/>, or as near it as fits on screen.</summary>
	internal static void Open(Vector2 at, PaintWheelConfig config)
		=> Center = WheelLayout.ClampScrapeCenter(at, Shape(PaintPicker.BuildSettings(config, 1f)), Main.screenWidth, Main.screenHeight);

	internal static int HitTest(in WheelLayout.Settings settings, Vector2 cursor, bool aimed)
		=> WheelLayout.HitTestScrape(Center, Shape(settings), cursor, aimed);

	internal static string Label(int option)
		=> Language.GetTextValue("Mods.PaintWheel.UI.Scrape." + Options[option]);

	internal static void Draw(SpriteBatch spriteBatch, PaintWheelConfig config, in WheelLayout.Settings settings, float opacity)
	{
		WheelLayout.ScrapeSettings shape = Shape(settings);

		if (config.Appearance.ShowBackgroundPanel)
			WheelDrawing.DrawPanel(spriteBatch, WheelLayout.ScrapePanel(Center, shape), opacity);

		string title = Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Header");
		Vector2 titleAt = WheelLayout.ScrapeTitle(Center, shape);
		int hovered = PickerInput.HoveredScrape;

		// The art - the title's rack, the buttons, the palette in the middle - unsmoothed, as the colour
		// wheel's is. The block and wall pictures are finer than that art's pixel, so they go on over it
		// with the text, smoothed.
		WheelDrawing.RestartBatch(spriteBatch, SamplerState.PointClamp);

		PickerRenderer.DrawRack(spriteBatch, titleAt, WheelDrawing.MeasureText(title, PaintPicker.HeaderTextScale).X,
			hovered: false, opacity);

		for (int i = 0; i < Options.Length; i++) {
			if (i != hovered)
				DrawOptionButton(spriteBatch, shape, i, hovered: false, opacity);
		}

		// Last, so the hover pop is never clipped by the neighbour drawn after it.
		if (hovered >= 0 && hovered < Options.Length)
			DrawOptionButton(spriteBatch, shape, hovered, hovered: true, opacity);

		DrawBack(spriteBatch, config, shape, hovered == WheelLayout.ScrapeBack, opacity);

		WheelDrawing.RestartBatch(spriteBatch, SamplerState.LinearClamp);

		WheelDrawing.DrawTextCentered(spriteBatch, title, titleAt, Main.OurFavoriteColor, opacity, PaintPicker.HeaderTextScale);

		for (int i = 0; i < Options.Length; i++)
			DrawOptionPicture(spriteBatch, config, shape, i, i == hovered, opacity);

		string name = hovered == WheelLayout.ScrapeBack ? Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Exit")
			: hovered >= 0 && hovered < Options.Length ? Label(hovered)
			: null;

		if (name is not null)
			WheelDrawing.DrawTextCentered(spriteBatch, name, WheelLayout.ScrapeInfo(Center, shape), Color.White, opacity, 0.9f);
	}

	/// <summary>
	/// An option's button, as the bottom row's: gold while it is the target, popping out and lit while
	/// hovered. A disc like a swatch's when the art did not load.
	/// </summary>
	private static void DrawOptionButton(SpriteBatch spriteBatch, in WheelLayout.ScrapeSettings shape, int index,
		bool hovered, float opacity)
	{
		Vector2 at = WheelLayout.ScrapeOption(Center, shape, index);
		float grow = shape.Progress * (hovered ? WheelLayout.HoverScale : 1f);
		bool target = Options[index] == PaintSelection.Scrape;
		Texture2D button = target ? UITextures.PickerRowActive : UITextures.PickerRowInactive;

		if (button is not null) {
			WheelDrawing.DrawCentered(spriteBatch, button, at, shape.Option / WheelLayout.RowButtonArtWidth * grow,
				(hovered ? Color.White : PickerRenderer.ArtRest) * opacity);
			return;
		}

		float radius = shape.Option * 0.5f * grow;
		if (radius <= 2f)
			return;

		PickerRenderer.DrawDiscBody(spriteBatch, at, radius, OptionFill, hovered, opacity);
		PickerRenderer.DrawDiscMarks(spriteBatch, at, radius, target, hovered, opacity);
	}

	/// <summary>What an option strips, on its button - the size the bottom row's icons are - with its number key.</summary>
	private static void DrawOptionPicture(SpriteBatch spriteBatch, PaintWheelConfig config, in WheelLayout.ScrapeSettings shape,
		int index, bool hovered, float opacity)
	{
		Vector2 at = WheelLayout.ScrapeOption(Center, shape, index);
		float grow = shape.Progress * (hovered ? WheelLayout.HoverScale : 1f);
		float radius = shape.Option * 0.5f * grow;

		if (radius <= 2f)
			return;

		DrawTarget(spriteBatch, Options[index], at, WheelLayout.PixelScale(config.Appearance.SwatchSize) * PictureSize * grow, opacity);

		// Where a swatch shows its number key: top left.
		PickerRenderer.DrawKeyHint(spriteBatch, config, index, at + new Vector2(-radius * 0.95f, -radius * 1.05f), opacity);
	}

	/// <summary>How wide an option's picture is, in the picker art's pixels: a bottom-row icon's width.</summary>
	private const float PictureSize = 13f;

	/// <summary>
	/// The middle: back to the colours, shown as the painter's palette the colour wheel opens its
	/// palettes from, outlined while hovered. A button, like the colour wheel's middle, so it answers a
	/// click rather than a release. A ring of paint on a disc when the art did not load.
	/// </summary>
	private static void DrawBack(SpriteBatch spriteBatch, PaintWheelConfig config, in WheelLayout.ScrapeSettings shape,
		bool hovered, float opacity)
	{
		Texture2D palette = hovered ? UITextures.PickerCenterHovered : UITextures.PickerCenter;

		if (palette is not null) {
			WheelDrawing.DrawCentered(spriteBatch, palette, Center,
				WheelLayout.PixelScale(config.Appearance.SwatchSize) * WheelLayout.ButtonGrow * shape.Progress, Color.White * opacity);
			return;
		}

		float radius = shape.Back * 0.5f * shape.Progress;
		if (radius <= 2f)
			return;

		PickerRenderer.DrawDiscBody(spriteBatch, Center, radius, PickerRenderer.CenterDiscFill, hovered, opacity);
		PickerRenderer.DrawDiscMarks(spriteBatch, Center, radius, chosen: false, hovered, opacity);

		float ring = radius * 0.52f;
		float dot = MathF.Max(2f, radius * 0.16f);

		for (int i = 0; i < BackColours.Length; i++) {
			Vector2 at = WheelMath.SectorPosition(Center, i, BackColours.Length, ring);

			WheelDrawing.DrawDisc(spriteBatch, at, dot + 1f, PickerRenderer.RimColor * opacity);
			WheelDrawing.DrawDisc(spriteBatch, at, dot, PaintCatalog.AccentColor(BackColours[i]) * opacity);
		}
	}

	/// <summary>
	/// What each option strips, shown with what it strips it from: the block, the wall, a block standing
	/// in front of its wall the way the world layers them, and a block under a coat.
	/// </summary>
	private static void DrawTarget(SpriteBatch spriteBatch, ScrapeMode mode, Vector2 center, float size, float opacity)
	{
		switch (mode) {
			case ScrapeMode.BlocksOnly:
				DrawBlock(spriteBatch, Square(center, size), opacity);
				break;

			case ScrapeMode.WallsOnly:
				DrawWall(spriteBatch, Square(center, size), opacity);
				break;

			case ScrapeMode.BlocksAndWalls: {
				float part = size * 0.72f;
				var shift = new Vector2(size * 0.16f);

				DrawWall(spriteBatch, Square(center - shift, part), opacity);

				// Its own dark edge, so the block stands off the wall behind it.
				Rectangle block = Square(center + shift, part);
				DrawEdge(spriteBatch, block, opacity);
				DrawBlock(spriteBatch, block, opacity);
				break;
			}

			default:
				DrawCoatedBlock(spriteBatch, Square(center, size), opacity);
				break;
		}
	}

	private static void DrawBlock(SpriteBatch spriteBatch, Rectangle box, float opacity)
		=> DrawImage(spriteBatch, UITextures.ScrapeBlock, box, BlockFallback, opacity);

	/// <summary>With a dark edge, which the wall needs to keep its square shape against the disc.</summary>
	private static void DrawWall(SpriteBatch spriteBatch, Rectangle box, float opacity)
	{
		DrawEdge(spriteBatch, box, opacity);
		DrawImage(spriteBatch, UITextures.ScrapeWall, box, WallFallback, opacity);
	}

	private static void DrawEdge(SpriteBatch spriteBatch, Rectangle box, float opacity)
		=> WheelDrawing.DrawRectOutline(spriteBatch, new Rectangle(box.X - 1, box.Y - 1, box.Width + 2, box.Height + 2),
			1, Color.Black * (opacity * 0.65f));

	/// <summary>The block under a coat: a pale glaze over its top, a bright edge and a glint - Illuminant's look.</summary>
	private static void DrawCoatedBlock(SpriteBatch spriteBatch, Rectangle box, float opacity)
	{
		DrawBlock(spriteBatch, box, opacity);

		WheelDrawing.DrawRect(spriteBatch, new Rectangle(box.X, box.Y, box.Width, box.Height * 2 / 5), Color.White * (opacity * 0.3f));
		WheelDrawing.DrawRectOutline(spriteBatch, box, 1, CoatEdge * opacity);

		int x = box.Right - 2;
		int y = box.Top + 2;
		WheelDrawing.DrawRect(spriteBatch, new Rectangle(x - 3, y, 7, 1), Color.White * opacity);
		WheelDrawing.DrawRect(spriteBatch, new Rectangle(x, y - 3, 1, 7), Color.White * opacity);
	}

	/// <summary>An image fitted to <paramref name="box"/>, or a flat colour until it has loaded.</summary>
	private static void DrawImage(SpriteBatch spriteBatch, Texture2D image, Rectangle box, Color fallback, float opacity)
	{
		if (image is null) {
			WheelDrawing.DrawRect(spriteBatch, box, fallback * opacity);
			return;
		}

		spriteBatch.Draw(image, box, Color.White * opacity);
	}

	private static Rectangle Square(Vector2 center, float size)
	{
		int side = Math.Max(2, (int)MathF.Round(size));

		return new Rectangle((int)MathF.Round(center.X - side * 0.5f), (int)MathF.Round(center.Y - side * 0.5f), side, side);
	}
}
