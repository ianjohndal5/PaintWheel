using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using PaintWheel.Common;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace PaintWheel.Configs;

/// <summary>Shape of the picker: a radial wheel, or a vertical strip of colour bands.</summary>
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

	/// <summary>Filipino, which Terraria itself cannot be set to. See Translations.</summary>
	Filipino,
}

/// <summary>How the chosen paint is forced onto vanilla's painting code. See PaintOverride.</summary>
public enum PaintOverrideMode
{
	/// <summary>Detour Player.FindPaintOrCoating. Nothing in the inventory is touched.</summary>
	Detour,

	/// <summary>Swap the chosen stack into the slot vanilla would pick, then swap it back. Fallback.</summary>
	InventorySwap,
}

/// <summary>
/// The client config. Only the settings people actually change sit on this page; sizes, the supply
/// readout and the compatibility switches live behind their own buttons.
/// </summary>
[BackgroundColor(64, 68, 148, 192)]
public class PaintWheelConfig : ModConfig
{
	public override ConfigScope Mode => ConfigScope.ClientSide;

	public static PaintWheelConfig Instance => ModContent.GetInstance<PaintWheelConfig>();

	// Above the first header, so it sits on its own at the top: it applies to every page below it.
	[DefaultValue(LanguageOverride.Automatic)]
	public LanguageOverride Language { get; set; } = LanguageOverride.Automatic;

	public override void OnChanged() => Translations.Apply();

	// ---- Input ------------------------------------------------------------------------------

	[Header("Input")]
	[DefaultValue(true)]
	public bool OpenWithRightClick { get; set; }

	[DefaultValue(PickerOpenMode.Hold)]
	public PickerOpenMode OpenMode { get; set; }

	[DefaultValue(true)]
	public bool RequirePaintTool { get; set; }

	[DefaultValue(true)]
	public bool PlaySounds { get; set; }

	// ---- Shape ------------------------------------------------------------------------------

	[Header("Shape")]
	[DefaultValue(WheelLayoutStyle.Wheel)]
	public WheelLayoutStyle Layout { get; set; }

	[SeparatePage]
	public AppearanceSettings Appearance { get; set; } = new();

	[SeparatePage]
	public SupplySettings Supply { get; set; } = new();

	[SeparatePage]
	public AdvancedSettings Advanced { get; set; } = new();

	// ---- Presets ----------------------------------------------------------------------------

	[Header("Presets")]
	public List<PaintPreset> Presets { get; set; } = new();
}

/// <summary>Sizes and cosmetics. Defaults are also set in code, so a nested group cannot come up empty.</summary>
public class AppearanceSettings
{
	[DefaultValue(false)]
	public bool ShowItemIcons { get; set; } = false;

	[DefaultValue(false)]
	public bool ShowBackgroundPanel { get; set; } = false;

	[Header("Wheel")]
	[DefaultValue(104)]
	[Range(50, 200)]
	[Increment(5)]
	[Slider]
	public int WheelRadius { get; set; } = 104;

	[DefaultValue(40)]
	[Range(24, 80)]
	[Increment(2)]
	[Slider]
	public int SwatchSize { get; set; } = 40;

	[DefaultValue(25)]
	[Range(0, 60)]
	[Slider]
	public int DeadZoneRadius { get; set; } = 25;

	[DefaultValue(12)]
	[Range(4, 12)]
	public int MaxSwatches { get; set; } = 12;

	[DefaultValue(7)]
	[Range(0, 20)]
	public int OpenAnimationTicks { get; set; } = 7;

	[Header("Bar")]
	[DefaultValue(58)]
	[Range(30, 140)]
	[Increment(2)]
	[Slider]
	public int BarCellWidth { get; set; } = 58;

	[DefaultValue(24)]
	[Range(12, 48)]
	[Increment(2)]
	[Slider]
	public int BarCellHeight { get; set; } = 24;

	[DefaultValue(16)]
	[Range(4, 24)]
	public int BarSwatchesPerPage { get; set; } = 16;
}

/// <summary>How a swatch shows what is left of that paint.</summary>
public class SupplySettings
{
	[DefaultValue(60)]
	[Range(1, 999)]
	public int FadeStartStack { get; set; } = 60;

	[DefaultValue(0.35f)]
	[Range(0f, 1f)]
	[Increment(0.05f)]
	[Slider]
	public float MinimumSwatchOpacity { get; set; } = 0.35f;

	[DefaultValue(true)]
	public bool ShowDepletionRing { get; set; } = true;

	[DefaultValue(12)]
	[Range(6, 24)]
	[Increment(2)]
	public int DepletionRingSegments { get; set; } = 12;

	[DefaultValue(false)]
	public bool HideEmptySwatches { get; set; } = false;
}

/// <summary>Painting behaviour, and the escape hatch for mod conflicts.</summary>
public class AdvancedSettings
{
	[DefaultValue(PaintOverrideMode.Detour)]
	public PaintOverrideMode OverrideMode { get; set; } = PaintOverrideMode.Detour;

	[DefaultValue(false)]
	public bool FallBackToAnyPaintWhenEmpty { get; set; } = false;

	[DefaultValue(true)]
	public bool ShowCoatingRow { get; set; } = true;

	[DefaultValue(true)]
	public bool SmartCoating { get; set; } = true;

	/// <summary>Grants Player.autoPaint - the Paint Sprayer's flag - so vanilla paints as you place.</summary>
	[DefaultValue(false)]
	public bool AutoPaintPlacedTiles { get; set; } = false;
}

/// <summary>
/// One saved palette. Rows start collapsed, so the list reads as one line per palette, and expand in
/// place for editing rather than opening a page - one click instead of two, and the list stays put.
/// </summary>
[Expand(false)]
public class PaintPreset
{
	/// <summary>How many colours the row shows before it gives up and counts the rest.</summary>
	private const int RowSwatchLimit = 12;

	[DefaultValue("Preset")]
	public string Name { get; set; } = "Preset";

	[CustomModConfigItem(typeof(PaintPaletteElement))]
	public List<ItemDefinition> Paints { get; set; } = new();

	/// <summary>
	/// The row label in the presets list. Config labels are drawn through ChatManager, which honours
	/// Terraria's inline colour tags, so the palette can show its actual colours here instead of a
	/// count that says nothing about what is in it.
	/// </summary>
	public override string ToString()
	{
		string name = string.IsNullOrWhiteSpace(Name) ? "Preset" : Name;

		if (Paints is null || Paints.Count == 0)
			return name + "  -";

		var label = new StringBuilder(name).Append("  ");
		int shown = 0;

		foreach (var definition in Paints) {
			if (definition is null || definition.IsUnloaded)
				continue;

			if (shown >= RowSwatchLimit)
				break;

			// One block per colour, and a '#' rather than a box glyph: the UI font is a sprite font with
			// a limited charset, so anything outside it would draw as a missing-character box.
			label.Append("[c/").Append(PaintCatalog.AccentColor(definition.Type).Hex3()).Append(":#]");
			shown++;
		}

		int remaining = Paints.Count - shown;
		if (remaining > 0)
			label.Append("  +").Append(remaining);

		return label.ToString();
	}

	public override bool Equals(object obj)
	{
		if (obj is not PaintPreset other || other.Name != Name)
			return false;

		int count = Paints?.Count ?? 0;
		if (count != (other.Paints?.Count ?? 0))
			return false;

		// Compared by contents, not just length: swapping one paint for another is a real change, and
		// a length-only check would report an edited preset as untouched.
		for (int i = 0; i < count; i++) {
			if (!Equals(Paints[i], other.Paints[i]))
				return false;
		}

		return true;
	}

	public override int GetHashCode() => (Name, Paints?.Count ?? 0).GetHashCode();
}
