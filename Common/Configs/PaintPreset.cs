using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using PaintWheel.Common.Configs.CustomUI;
using PaintWheel.Common.Systems;
using Terraria;
using Terraria.ModLoader.Config;

namespace PaintWheel.Common.Configs;

/// <summary>
/// One saved palette. Rows start collapsed, so the list reads as one line per palette, and expand in
/// place for editing rather than opening a page - one click instead of two, and the list stays put.
/// </summary>
[Expand(false)]
public class PaintPreset
{
	/// <summary>How many colours the row shows before it gives up and counts the rest.</summary>
	private const int RowSwatchLimit = 12;

	/// <summary>The name a palette gets when none is given. It was "Preset" before palettes were called palettes.</summary>
	public const string DefaultName = "Palette";

	/// <summary>The old default, still what a character may remember a default-named palette by.</summary>
	public const string LegacyDefaultName = "Preset";

	[DefaultValue(DefaultName)]
	public string Name { get; set; } = DefaultName;

	[CustomModConfigItem(typeof(PaintPaletteElement))]
	public List<ItemDefinition> Paints { get; set; } = new();

	/// <summary>
	/// Where <paramref name="itemType"/> sits among the paints the palette really shows - loaded, real
	/// paints, each counted once - which is its place in the ring. 1-based; 0 when it is not in it.
	/// </summary>
	public static int RingOrder(List<ItemDefinition> paints, int itemType)
	{
		if (paints is null || itemType <= 0)
			return 0;

		int position = 0;
		var seen = new HashSet<int>();

		foreach (ItemDefinition definition in paints) {
			int type = definition?.IsUnloaded == false ? definition.Type : 0;
			if (type <= 0 || !PaintCatalog.IsPaint(type) || !seen.Add(type))
				continue;

			position++;
			if (type == itemType)
				return position;
		}

		return 0;
	}

	/// <summary>
	/// The row label in the presets list. Config labels are drawn through ChatManager, which honours
	/// Terraria's inline colour tags, so the palette can show its actual colours here instead of a
	/// count that says nothing about what is in it.
	/// </summary>
	public override string ToString()
	{
		string name = string.IsNullOrWhiteSpace(Name) ? DefaultName : Name;

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
}
