using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace PaintWheel.Common.Systems;

/// <summary>
/// Every keybind the mod registers. The names are what Controls saves your bindings under, and the
/// registration order is the order they are listed there, so both are kept stable. None of them has a
/// default key: a mod that claims keys out of the box clashes with whatever else is installed, and
/// right click already opens the picker. Bindings a player has already saved are kept by the game.
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

		OpenWheelKey = KeybindLoader.RegisterKeybind(Mod, "OpenWheel", Unbound);
		EyedropperKey = KeybindLoader.RegisterKeybind(Mod, "Eyedropper", Unbound);
		QuickToggleKey = KeybindLoader.RegisterKeybind(Mod, "QuickToggleLastTwo", Unbound);
		PreviousPaletteKey = KeybindLoader.RegisterKeybind(Mod, "PreviousPalette", Unbound);
		NextPaletteKey = KeybindLoader.RegisterKeybind(Mod, "NextPalette", Unbound);
		ToggleScrapeKey = KeybindLoader.RegisterKeybind(Mod, "ToggleScrape", Unbound);
		ToggleNoPaintKey = KeybindLoader.RegisterKeybind(Mod, "ToggleNoPaint", Unbound);

		// Added later, so after the first seven: the order is the Controls order.
		PreviousPaintKey = KeybindLoader.RegisterKeybind(Mod, "PreviousPaint", Unbound);
		NextPaintKey = KeybindLoader.RegisterKeybind(Mod, "NextPaint", Unbound);
		SwapToolKey = KeybindLoader.RegisterKeybind(Mod, "SwapPaintTool", Unbound);
		PaintBothKey = KeybindLoader.RegisterKeybind(Mod, "TogglePaintBoth", Unbound);
	}

	/// <summary>
	/// The default that means no key. tModLoader refuses an empty one, and lists "None" in Controls -
	/// the name of the key the keyboard never reports as pressed - so this is how a keybind starts unbound.
	/// </summary>
	private const string Unbound = "None";

	/// <summary>
	/// The keys bound to <paramref name="keybind"/>, leaving out <see cref="Unbound"/>, which is a placeholder
	/// rather than a key: "Reset to default" in Controls puts it back.
	/// </summary>
	public static List<string> BoundKeys(ModKeybind keybind)
	{
		var keys = new List<string>();

		foreach (string key in keybind?.GetAssignedKeys() ?? new List<string>()) {
			if (!string.IsNullOrEmpty(key) && key != Unbound)
				keys.Add(key);
		}

		return keys;
	}

	/// <summary>Whether <paramref name="keybind"/> has a real key bound, so a hint about it is worth showing.</summary>
	public static bool IsBound(ModKeybind keybind) => BoundKeys(keybind).Count > 0;

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
