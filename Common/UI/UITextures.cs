using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;

namespace PaintWheel.Common.UI;

/// <summary>
/// The textures the mod's UI draws: vanilla's own button art where it has some - the same the
/// character and world lists use - and the mod's own in Assets/Textures. Loaded on first use; null until
/// loaded, so every caller keeps a hand-drawn fallback.
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

	private static Texture2D Ready(Asset<Texture2D> asset) => asset is { IsLoaded: true } ? asset.Value : null;

	public static void Unload()
	{
		rename = null;
		delete = null;
		paintBoth = null;
	}
}
