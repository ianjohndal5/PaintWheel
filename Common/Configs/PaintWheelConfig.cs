using System.Collections.Generic;
using System.ComponentModel;
using PaintWheel.Common.Systems;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace PaintWheel.Common.Configs;

/// <summary>
/// The client config. Only the settings people actually change sit on this page; sizes, the supply
/// readout and the compatibility switches live behind their own buttons, as the
/// <see cref="AppearanceSettings"/>, <see cref="SupplySettings"/> and <see cref="AdvancedSettings"/> pages below.
/// </summary>
[BackgroundColor(64, 68, 148, 192)]
public class PaintWheelConfig : ModConfig
{
	public override ConfigScope Mode => ConfigScope.ClientSide;

	public static PaintWheelConfig Instance => ModContent.GetInstance<PaintWheelConfig>();

	// Above the first header, so it sits on its own at the top: it applies to every page below it.
	[DefaultValue(LanguageOverride.Automatic)]
	public LanguageOverride Language { get; set; } = LanguageOverride.Automatic;

	public override void OnChanged() => TranslationSystem.Apply();

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

	[DefaultValue(true)]
	public bool ShowQuickKeys { get; set; } = true;

	[DefaultValue(false)]
	public bool ShowStackCounts { get; set; } = false;

	[DefaultValue(true)]
	public bool ShowCursorReadout { get; set; } = true;

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
	[Slider]
	public int MaxSwatches { get; set; } = 12;

	[DefaultValue(7)]
	[Range(0, 20)]
	[Slider]
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
	[Slider]
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
	[Slider]
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
