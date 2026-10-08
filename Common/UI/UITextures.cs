using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace PaintWheel.Common.UI;

/// <summary>
/// The mod's own art, from Assets/Textures: the paint-both icon, the wood block and wall the scrape
/// wheel shows, the palette board's pieces and the picker's. Loaded on first use; null until loaded, so
/// every caller keeps a fallback.
/// </summary>
public static class UITextures
{
	private static readonly Dictionary<string, Asset<Texture2D>> assets = new();

	/// <summary>The paint-both switch: a block and a wall split corner to corner, one stripe of paint across both.</summary>
	public static Texture2D PaintBoth => Get("PaintBoth");

	/// <summary>A wood block, for the scrape wheel's options that strip blocks.</summary>
	public static Texture2D ScrapeBlock => Get("ScrapeBlock");

	/// <summary>A wood wall, for the scrape wheel's options that strip walls.</summary>
	public static Texture2D ScrapeWall => Get("ScrapeWall");

	/// <summary>The palette board itself, with its "Palette" tab and the wells the pills, bar and paints sit in.</summary>
	public static Texture2D PaletteBoard => Get("Palette/Board");

	public static Texture2D PaletteActivePill => Get("Palette/PillActive");

	public static Texture2D PaletteInactivePill => Get("Palette/PillInactive");

	public static Texture2D PaletteScrollThumb => Get("Palette/ScrollThumb");

	/// <summary>A paint's frame when the palette has it: gold, with a hole the colour shows through.</summary>
	public static Texture2D PaletteSlotUsed => Get("Palette/SlotUsed");

	/// <summary>The same frame for a paint the palette does not have.</summary>
	public static Texture2D PaletteSlotUnused => Get("Palette/SlotUnused");

	public static Texture2D PaletteExit => Get("Palette/ButtonExit");

	public static Texture2D PaletteAdd => Get("Palette/ButtonAdd");

	public static Texture2D PaletteDelete => Get("Palette/ButtonDelete");

	public static Texture2D PaletteRename => Get("Palette/ButtonRename");

	/// <summary>The arrows beside the paints, for the rows above and below the two on show.</summary>
	public static Texture2D PaletteArrowUp => Get("Palette/ArrowUp");

	public static Texture2D PaletteArrowDown => Get("Palette/ArrowDown");

	/// <summary>The ring round the paint in use on the wheel: gold, hollow, the colour showing through.</summary>
	public static Texture2D PickerPaintActive => Get("Picker/Wheel/activepaint");

	/// <summary>The ring round every other paint on the wheel.</summary>
	public static Texture2D PickerPaintInactive => Get("Picker/Wheel/inactivepaint");

	/// <summary>The wheel's middle, which opens the palette board: a painter's palette.</summary>
	public static Texture2D PickerCenter => Get("Picker/Wheel/centerbutton");

	public static Texture2D PickerCenterHovered => Get("Picker/Wheel/hoveredcenterbutton");

	/// <summary>A bottom-row button that is on - the coating in use, a switch that is set.</summary>
	public static Texture2D PickerRowActive => Get("Picker/acriverowbutton");

	public static Texture2D PickerRowInactive => Get("Picker/inactiverowbutton");

	public static Texture2D PickerNoCoating => Get("Picker/nocoatingicon");

	public static Texture2D PickerIlluminant => Get("Picker/illumunantcoatingicon");

	public static Texture2D PickerEcho => Get("Picker/echocoatingicon");

	/// <summary>Bare placement: a block with no paint on it.</summary>
	public static Texture2D PickerPaintTarget => Get("Picker/painttargeticon");

	public static Texture2D PickerScrape => Get("Picker/scrapemodeicon");

	/// <summary>The rack the palette's name sits on above the picker.</summary>
	public static Texture2D PickerNameRack => Get("Picker/palletenamerack");

	/// <summary>The page arrows either side of the picker.</summary>
	public static Texture2D PickerArrowLeft => Get("Picker/arrowleft");

	public static Texture2D PickerArrowRight => Get("Picker/arrowright");

	private static Texture2D Get(string name)
	{
		if (!assets.TryGetValue(name, out Asset<Texture2D> asset))
			assets[name] = asset = ModContent.Request<Texture2D>("PaintWheel/Assets/Textures/" + name, AssetRequestMode.ImmediateLoad);

		return asset is { IsLoaded: true } ? asset.Value : null;
	}

	private static readonly Dictionary<Texture2D, Texture2D> fills = new();

	/// <summary>
	/// The inside of a ring sprite as a white silhouette, to tint with a colour and draw under the ring:
	/// every pixel not reached from the sprite's edge through clear ones. Made once, from the ring's own
	/// pixels, so a redrawn ring needs no mask drawn to match. Main thread only - the picker draws there.
	/// </summary>
	public static Texture2D FillOf(Texture2D ring)
	{
		if (ring is null)
			return null;

		if (fills.TryGetValue(ring, out Texture2D fill))
			return fill;

		int width = ring.Width;
		int height = ring.Height;
		var pixels = new Color[width * height];
		ring.GetData(pixels);

		var outside = new bool[pixels.Length];
		var open = new Stack<int>();

		for (int x = 0; x < width; x++) {
			Reach(x, 0);
			Reach(x, height - 1);
		}

		for (int y = 0; y < height; y++) {
			Reach(0, y);
			Reach(width - 1, y);
		}

		while (open.Count > 0) {
			int at = open.Pop();
			int x = at % width;
			int y = at / width;

			Reach(x + 1, y);
			Reach(x - 1, y);
			Reach(x, y + 1);
			Reach(x, y - 1);
		}

		for (int i = 0; i < pixels.Length; i++)
			pixels[i] = outside[i] ? Color.Transparent : Color.White;

		fill = new Texture2D(Main.instance.GraphicsDevice, width, height);
		fill.SetData(pixels);
		fills[ring] = fill;
		return fill;

		void Reach(int x, int y)
		{
			if (x < 0 || y < 0 || x >= width || y >= height)
				return;

			int at = y * width + x;
			if (outside[at] || pixels[at].A != 0)
				return;

			outside[at] = true;
			open.Push(at);
		}
	}

	public static void Unload()
	{
		assets.Clear();

		// Made here rather than loaded, so they are this mod's to dispose - on the main thread, as the
		// graphics device wants.
		Texture2D[] made = fills.Values.ToArray();
		fills.Clear();

		if (made.Length > 0)
			Main.QueueMainThreadAction(() => {
				foreach (Texture2D texture in made)
					texture.Dispose();
			});
	}
}
