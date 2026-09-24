namespace PaintWheel.Common.Configs;

/// <summary>Shape of the picker: a radial wheel, a vertical strip of colour bands, or a grid of every swatch.</summary>
public enum WheelLayoutStyle
{
	/// <summary>Swatches arranged in a ring, chosen by flicking in a direction.</summary>
	Wheel,

	/// <summary>Swatches stacked in a column beside the cursor, chosen by sweeping down it.</summary>
	Bar,

	/// <summary>Every swatch laid out in rows, so a full palette needs no paging.</summary>
	Grid,
}

/// <summary>Whether the picker is held open or left open.</summary>
public enum PickerOpenMode
{
	/// <summary>Hold right click, flick, release to choose. Nothing stays on screen.</summary>
	Hold,

	/// <summary>One right click leaves it up until a paint is chosen, right click again, or Escape.</summary>
	Click,
}

/// <summary>
/// Which language the mod's own text uses. Every entry other than Automatic is read from this mod's
/// own packed files, so the mod can speak one language while the game speaks another.
/// </summary>
public enum LanguageOverride
{
	/// <summary>Follow the game's language setting.</summary>
	Automatic,

	English,
	German,
	Spanish,
	French,
	Italian,
	Polish,
	Portuguese,
	Russian,
	Chinese,

	/// <summary>Filipino, which Terraria itself cannot be set to. See <see cref="Systems.TranslationSystem"/>.</summary>
	Filipino,
}

/// <summary>How the chosen paint is forced onto vanilla's painting code. See <see cref="Systems.PaintOverrideSystem"/>.</summary>
public enum PaintOverrideMode
{
	/// <summary>Detour Player.FindPaintOrCoating. Nothing in the inventory is touched.</summary>
	Detour,

	/// <summary>Swap the chosen stack into the slot vanilla would pick, then swap it back. Fallback.</summary>
	InventorySwap,
}
