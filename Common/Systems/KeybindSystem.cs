using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;

namespace PaintWheel.Common.Systems;

/// <summary>
/// Every keybind the mod registers. The names are what Controls saves your bindings under, and the
/// registration order is the order they are listed there, so both are kept stable.
/// </summary>
public class KeybindSystem : ModSystem
{
	/// <summary>Hold to open the picker and release to commit, or tap it to leave the picker open. Alternative to right click.</summary>
	public static ModKeybind OpenWheelKey { get; private set; }

	/// <summary>Copies the paint off the inventory slot, tile or wall under the cursor.</summary>
	public static ModKeybind EyedropperKey { get; private set; }

	/// <summary>Flips between the two paints you used most recently.</summary>
	public static ModKeybind QuickToggleKey { get; private set; }

	/// <summary>Steps to the next palette without opening the picker.</summary>
	public static ModKeybind NextPaletteKey { get; private set; }

	/// <summary>Steps to the previous palette without opening the picker.</summary>
	public static ModKeybind PreviousPaletteKey { get; private set; }

	/// <summary>Enters or leaves scrape mode without opening the picker.</summary>
	public static ModKeybind ToggleScrapeKey { get; private set; }

	/// <summary>Flips between placing blocks bare and painting them again.</summary>
	public static ModKeybind ToggleNoPaintKey { get; private set; }

	/// <summary>Steps to the previous colour in the palette, without the picker.</summary>
	public static ModKeybind PreviousPaintKey { get; private set; }

	/// <summary>Steps to the next colour in the palette, without the picker.</summary>
	public static ModKeybind NextPaintKey { get; private set; }

	/// <summary>Trades the brush in hand for a roller, or the roller for a brush.</summary>
	public static ModKeybind SwapToolKey { get; private set; }

	/// <summary>Turns on or off painting blocks and walls with either tool.</summary>
	public static ModKeybind PaintBothKey { get; private set; }

	public override void Load()
	{
		if (Main.dedServ)
			return;

		OpenWheelKey = KeybindLoader.RegisterKeybind(Mod, "OpenWheel", Keys.V.ToString());
		EyedropperKey = KeybindLoader.RegisterKeybind(Mod, "Eyedropper", "Mouse3");
		QuickToggleKey = KeybindLoader.RegisterKeybind(Mod, "QuickToggleLastTwo", Keys.X.ToString());

		// Bracket keys: they read as previous/next, and nothing in vanilla claims them.
		PreviousPaletteKey = KeybindLoader.RegisterKeybind(Mod, "PreviousPalette", Keys.OemOpenBrackets.ToString());
		NextPaletteKey = KeybindLoader.RegisterKeybind(Mod, "NextPalette", Keys.OemCloseBrackets.ToString());

		// G: unclaimed by vanilla, and next to the movement keys rather than across the board.
		ToggleScrapeKey = KeybindLoader.RegisterKeybind(Mod, "ToggleScrape", Keys.G.ToString());
		ToggleNoPaintKey = KeybindLoader.RegisterKeybind(Mod, "ToggleNoPaint", Keys.N.ToString());

		// Added later, so after the first seven: the order is the Controls order, and the names are what
		// bindings are saved under. Comma and period read as a step either way; Q and Z sit by the
		// movement keys. None of the four is claimed by vanilla.
		PreviousPaintKey = KeybindLoader.RegisterKeybind(Mod, "PreviousPaint", Keys.OemComma.ToString());
		NextPaintKey = KeybindLoader.RegisterKeybind(Mod, "NextPaint", Keys.OemPeriod.ToString());
		SwapToolKey = KeybindLoader.RegisterKeybind(Mod, "SwapPaintTool", Keys.Q.ToString());
		PaintBothKey = KeybindLoader.RegisterKeybind(Mod, "TogglePaintBoth", Keys.Z.ToString());
	}

	public override void Unload()
	{
		OpenWheelKey = null;
		EyedropperKey = null;
		QuickToggleKey = null;
		NextPaletteKey = null;
		PreviousPaletteKey = null;
		ToggleScrapeKey = null;
		ToggleNoPaintKey = null;
		PreviousPaintKey = null;
		NextPaintKey = null;
		SwapToolKey = null;
		PaintBothKey = null;
	}
}
