using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;

namespace PaintWheel.Common.UI;

/// <summary>
/// The textures the mod's UI draws: vanilla's own button art where it has some - the same the
/// character and world lists use - the world's own wood block and wall for the scrape wheel, and the
/// mod's own in Assets/Textures. Loaded on first use; null until loaded, so every caller keeps a
/// fallback.
/// </summary>
public static class UITextures
{
	private static Asset<Texture2D> rename;
	private static Asset<Texture2D> delete;
	private static Asset<Texture2D> paintBoth;

	public static Texture2D Rename => Ready(rename ??= Main.Assets.Request<Texture2D>("Images/UI/ButtonRename", AssetRequestMode.ImmediateLoad));

	public static Texture2D Delete => Ready(delete ??= Main.Assets.Request<Texture2D>("Images/UI/ButtonDelete", AssetRequestMode.ImmediateLoad));

	/// <summary>tModLoader's config "+", which vanilla has no equivalent of.</summary>
	public static Texture2D Plus => Ready(UICommon.ButtonPlusTexture);

	/// <summary>The paint-both switch: a block and a wall split corner to corner, one stripe of paint across both.</summary>
	public static Texture2D PaintBoth => Ready(paintBoth ??= ModContent.Request<Texture2D>("PaintWheel/Assets/Textures/PaintBoth", AssetRequestMode.ImmediateLoad));

	/// <summary>
	/// The Wood block's sprite sheet, whatever the world draws it with - a resource pack's or another
	/// mod's included - loaded the way vanilla loads it; <see cref="BlockFrame"/> is one block of it.
	/// </summary>
	public static Texture2D WoodBlock
	{
		get {
			Main.instance.LoadTiles(TileID.WoodBlock);
			return Ready(TextureAssets.Tile[TileID.WoodBlock]);
		}
	}

	/// <summary>The same for the Wood Wall; <see cref="WallFrame"/> is one piece of it.</summary>
	public static Texture2D WoodWall
	{
		get {
			Main.instance.LoadWall(WallID.Wood);
			return Ready(TextureAssets.Wall[WallID.Wood]);
		}
	}

	/// <summary>A block with neighbours on every side, so it has no edge drawn on it: 16 pixels on an 18 pixel grid.</summary>
	public static readonly Rectangle BlockFrame = new(18, 18, 16, 16);

	/// <summary>
	/// The same for a wall: a 32 pixel piece on a 36 pixel grid, whose border only reaches over to
	/// neighbours - so for a wall surrounded on every side, just the 16 pixels in the middle are drawn.
	/// </summary>
	public static readonly Rectangle WallFrame = new(44, 44, 16, 16);

	private static Texture2D Ready(Asset<Texture2D> asset) => asset is { IsLoaded: true } ? asset.Value : null;

	public static void Unload()
	{
		rename = null;
		delete = null;
		paintBoth = null;
	}
}
