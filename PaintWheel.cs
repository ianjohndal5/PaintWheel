using Microsoft.Xna.Framework.Input;
using PaintWheel.Common;
using Terraria;
using Terraria.ModLoader;

namespace PaintWheel;

public class PaintWheel : Mod
{
	/// <summary>The loaded mod instance, for logging from static helpers.</summary>
	public static PaintWheel Instance { get; private set; }

	/// <summary>Hold to open the wheel; release to commit. Alternative to right click.</summary>
	public static ModKeybind OpenWheelKey { get; private set; }

	/// <summary>Copies the paint off whatever tile or wall the cursor is over.</summary>
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

	public override void Load()
	{
		Instance = this;

		if (!Main.dedServ) {
			OpenWheelKey = KeybindLoader.RegisterKeybind(this, "OpenWheel", Keys.V.ToString());
			EyedropperKey = KeybindLoader.RegisterKeybind(this, "Eyedropper", "Mouse3");
			QuickToggleKey = KeybindLoader.RegisterKeybind(this, "QuickToggleLastTwo", Keys.X.ToString());

			// Bracket keys: they read as previous/next, and nothing in vanilla claims them.
			PreviousPaletteKey = KeybindLoader.RegisterKeybind(this, "PreviousPalette", Keys.OemOpenBrackets.ToString());
			NextPaletteKey = KeybindLoader.RegisterKeybind(this, "NextPalette", Keys.OemCloseBrackets.ToString());

			// G: unclaimed by vanilla, and next to the movement keys rather than across the board.
			ToggleScrapeKey = KeybindLoader.RegisterKeybind(this, "ToggleScrape", Keys.G.ToString());
			ToggleNoPaintKey = KeybindLoader.RegisterKeybind(this, "ToggleNoPaint", Keys.N.ToString());
		}

		Translations.Load(this);
		PaintOverride.Load();
	}

	public override void Unload()
	{
		PaintOverride.Unload();
		PaintHover.Reset();
		Translations.Unload();

		OpenWheelKey = null;
		EyedropperKey = null;
		QuickToggleKey = null;
		NextPaletteKey = null;
		PreviousPaletteKey = null;
		ToggleScrapeKey = null;
		ToggleNoPaintKey = null;
		Instance = null;
	}
}
