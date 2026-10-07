using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace PaintWheel.Common.UI;

/// <summary>
/// The mod's own art, from Assets/Textures: the paint-both icon, the wood block and wall the scrape
/// wheel shows, and the palette board's pieces. Loaded on first use; null until loaded, so every caller
/// keeps a fallback.
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

	private static Texture2D Get(string name)
	{
		if (!assets.TryGetValue(name, out Asset<Texture2D> asset))
			assets[name] = asset = ModContent.Request<Texture2D>("PaintWheel/Assets/Textures/" + name, AssetRequestMode.ImmediateLoad);

		return asset is { IsLoaded: true } ? asset.Value : null;
	}

	public static void Unload() => assets.Clear();
}
