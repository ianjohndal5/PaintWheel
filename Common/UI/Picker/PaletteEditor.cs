using System;
using System.Collections.Generic;
using System.Linq;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// What is in the palette on the palette board, and every change to the saved palettes - adding a
/// paint, making, renaming and deleting a palette - which all end in writing the config. The board
/// (<see cref="PaletteBoard"/>) is the drawing and the clicks.
/// </summary>
internal static class PaletteEditor
{
	/// <summary>Which config preset is on the board, or -1 for the automatic palette.</summary>
	private static int presetIndex = -1;

	/// <summary>The preset "+" just made, until the board is left, so one left empty can be thrown away.</summary>
	private static int newPreset = -1;

	/// <summary>Every paint the game has, in catalog order: the board's slots.</summary>
	internal static readonly List<int> GridPaints = new();

	/// <summary>The paints in the palette, for asking "is this one in it".</summary>
	internal static readonly HashSet<int> Members = new();

	/// <summary>The same paints in the palette's own order, which is the order they sit in the ring.</summary>
	private static readonly List<int> order = new();

	/// <summary>True while a saved palette is on the board, so its paints can be changed.</summary>
	internal static bool Editing => presetIndex >= 0;

	internal static void StopEditing()
	{
		presetIndex = -1;
		newPreset = -1;
		GridPaints.Clear();
		Members.Clear();
		order.Clear();
	}

	/// <summary>
	/// Puts a saved palette on the board: every paint the game has, with the palette's own marked.
	/// False when <paramref name="index"/> is not a saved palette.
	/// </summary>
	internal static bool Begin(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= config.Presets.Count)
			return false;

		presetIndex = index;
		FillGrid();
		ReadMembers(config.Presets[index]);
		return true;
	}

	/// <summary>
	/// Puts the automatic palette on the board: the same grid, with what it holds marked - all of it,
	/// including paints the setting that hides empty swatches leaves off the wheel. Its changes are the
	/// character's, not the config's, so they go through the player rather than through here.
	/// </summary>
	internal static void ShowAutomatic()
	{
		presetIndex = -1;
		FillGrid();
		Members.Clear();
		order.Clear();

		foreach (int type in PickerContent.AutomaticPaints) {
			if (Members.Add(type))
				order.Add(type);
		}
	}

	/// <summary>
	/// Every paint the game has, in the order the config grid uses - a palette is worth building out of
	/// colours you have not bought yet.
	/// </summary>
	private static void FillGrid()
	{
		GridPaints.Clear();
		GridPaints.AddRange(PaintCatalog.Paints);
	}

	/// <summary>A paint's place in the palette on the board, counting from 1 - its place in the ring - or 0.</summary>
	internal static int PlaceOf(int type) => order.IndexOf(type) + 1;

	/// <summary>How many paints a palette holds - -1 for the automatic one - as the ring will show them.</summary>
	internal static int CountOf(int preset, PaintWheelConfig config)
	{
		if (preset < 0 || preset >= config.Presets.Count) {
			foreach (Palette palette in PickerContent.Palettes) {
				if (palette.Key == PickerContent.OwnedPaletteKey)
					return palette.Paints.Count;
			}

			return 0;
		}

		var seen = new HashSet<int>();
		foreach (ItemDefinition definition in config.Presets[preset]?.Paints ?? new List<ItemDefinition>()) {
			int type = definition?.IsUnloaded == false ? definition.Type : 0;
			if (type > 0 && PaintCatalog.IsPaint(type))
				seen.Add(type);
		}

		return seen.Count;
	}

	/// <summary>
	/// Adds a paint to the end of the palette or takes it out, and saves. The rest of the list is left
	/// exactly as it was - its order is the ring's order, and an entry from a mod that is switched off
	/// right now is still the player's, to come back when that mod does.
	/// </summary>
	internal static void ToggleMember(int type, PaintWheelConfig config)
	{
		if (!Editing || type <= 0 || presetIndex >= config.Presets.Count)
			return;

		// Fetched fresh each time: saving reads the file back into the config, replacing these objects.
		PaintPreset preset = config.Presets[presetIndex] ??= new PaintPreset();
		preset.Paints ??= new List<ItemDefinition>();

		if (Members.Contains(type))
			preset.Paints.RemoveAll(definition => definition is { IsUnloaded: false } && definition.Type == type);
		else
			preset.Paints.Add(new ItemDefinition(type));

		Save(config);

		if (presetIndex < config.Presets.Count)
			ReadMembers(config.Presets[presetIndex]);
	}

	/// <summary>The palette's paints as the ring will show them: loaded, real paints, each once.</summary>
	private static void ReadMembers(PaintPreset preset)
	{
		Members.Clear();
		order.Clear();

		foreach (ItemDefinition definition in preset?.Paints ?? new List<ItemDefinition>()) {
			int type = definition?.IsUnloaded == false ? definition.Type : 0;
			if (type > 0 && PaintCatalog.IsPaint(type) && Members.Add(type))
				order.Add(type);
		}
	}

	private static readonly List<int> owned = new();

	/// <summary>
	/// Adds a palette of what you are carrying - an empty one when that is nothing, to fill in on the
	/// board - and makes it the one in use once it has a colour.
	/// </summary>
	/// <returns>Its index among the presets, or -1 when it could not be saved.</returns>
	internal static int CreatePreset(PaintWheelConfig config)
	{
		PaintInventory.CollectOwnedPaints(Main.LocalPlayer, owned);

		var preset = new PaintPreset { Name = NextPresetName(config) };
		foreach (int type in owned)
			preset.Paints.Add(new ItemDefinition(type));

		config.Presets.Add(preset);
		int index = config.Presets.Count - 1;
		bool saved = Save(config);

		// In use once it has a colour. An empty one is nothing the wheel can show, so the palette in use
		// stays - and stays put if this one is thrown away unfilled.
		if (saved && preset.Paints.Count > 0)
			PaintSelection.SetPalette(preset.Name);

		// Rebuilt whether or not the save went through, so the wheel always matches the presets in memory.
		PickerContent.RebuildPalettes(config);
		PickerContent.ApplyPage(config);

		if (!saved || index >= config.Presets.Count)
			return -1;

		newPreset = index;
		return index;
	}

	/// <summary>
	/// Throws away the palette "+" made if it is being left - for another palette, or by leaving the
	/// board - with nothing in it. Returns the place it had, or -1 when nothing went, so a caller
	/// holding a later place can step it back.
	/// </summary>
	internal static int DiscardIfEmptyNew(PaintWheelConfig config)
	{
		// Only while it is the one on the board: "+" marks its palette before the board moves onto it.
		int preset = newPreset;
		if (preset < 0 || preset != presetIndex)
			return -1;

		newPreset = -1;

		if (preset >= config.Presets.Count
			|| config.Presets[preset]?.Paints?.Exists(definition => definition is { IsUnloaded: false }) == true)
			return -1;

		// Back to the palette that is always there only when this was the one in use: one that was
		// filled with what you carry and then emptied again.
		bool inUse = PaintSelection.Palette == PickerContent.PaletteKey(preset, config);

		config.Presets.RemoveAt(preset);
		presetIndex = -1;
		Save(config);

		if (inUse)
			PaintSelection.SetPalette(PickerContent.OwnedPaletteKey);

		return preset;
	}

	/// <summary>
	/// Renames a saved palette and saves. Palettes are keyed by name, so the one in use - this one, or
	/// one sharing its old name - is followed by its place in the list rather than its old key.
	/// </summary>
	internal static void RenamePreset(int preset, string name, PaintWheelConfig config)
	{
		if (preset < 0 || preset >= config.Presets.Count)
			return;

		int shownPreset = PickerContent.ActivePalette?.Preset ?? -1;
		int shownPage = PickerContent.PageIndex;

		(config.Presets[preset] ??= new PaintPreset()).Name = name;
		Save(config);

		PickerContent.RebuildPalettes(config);

		if (shownPreset >= 0)
			PickerContent.FollowPreset(shownPreset, shownPage, config);

		PickerContent.ApplyPage(config);
	}

	internal static bool DeletePreset(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= config.Presets.Count)
			return false;

		int shownPreset = PickerContent.ActivePalette?.Preset ?? -1;
		int shownPage = PickerContent.PageIndex;

		config.Presets.RemoveAt(index);
		StopEditing();
		bool saved = Save(config);

		// Back to the palette that is always there, but only when the one being shown is what went:
		// deleting some other palette should not move you off the one you are using.
		if (shownPreset == index)
			PaintSelection.SetPalette(PickerContent.OwnedPaletteKey);

		// Rebuilt whether or not the save went through, so the wheel always matches the presets in memory.
		PickerContent.RebuildPalettes(config);

		// Palettes sharing a name are keyed by their order, so taking out an earlier one renumbers the
		// ones after it. The one being shown is followed by its place in the list, not its old key.
		if (shownPreset > index)
			PickerContent.FollowPreset(shownPreset - 1, shownPage, config);

		PickerContent.ApplyPage(config);

		return saved;
	}

	/// <summary>
	/// Saves the config the way its own page does. SaveChanges writes the file, reads it back and
	/// raises OnChanged, so the edit survives a restart and nothing else has to be told about it.
	/// </summary>
	private static bool Save(PaintWheelConfig config)
	{
		try {
			return config.SaveChanges() == ConfigSaveResult.Success;
		}
		catch (Exception exception) {
			PaintWheel.Instance?.Logger.Error("Could not save the edited palette.", exception);
			return false;
		}
	}

	/// <summary>
	/// A name no other palette is using. Presets are matched by name, so two called the same thing
	/// would be one palette as far as switching is concerned.
	/// </summary>
	private static string NextPresetName(PaintWheelConfig config)
	{
		string label = Language.GetTextValue("Mods.PaintWheel.UI.NewPaletteName");

		for (int n = 1; n < 100; n++) {
			string name = $"{label} {n}";
			if (!config.Presets.Any(preset => preset?.Name == name))
				return name;
		}

		return label;
	}
}
