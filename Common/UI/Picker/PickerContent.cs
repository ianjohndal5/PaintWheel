using System;
using System.Collections.Generic;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using Terraria;
using Terraria.Localization;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// What the picker is offering right now: the palettes, the page of swatches on show and their stack
/// counts, and the bottom row of coatings and mode buttons. No drawing and no input - the rest of the
/// picker reads this.
/// </summary>
internal static class PickerContent
{
	/// <summary>Key of the auto-filled palette. Prefixed so it cannot collide with a preset name.</summary>
	public const string OwnedPaletteKey = "*owned";

	internal static readonly List<Palette> Palettes = new();
	internal static readonly List<int> Swatches = new();
	internal static readonly List<int> SwatchStacks = new();
	internal static readonly List<int> CoatingRow = new();
	private static readonly List<int> scratch = new();
	private static readonly List<int> paletteScratch = new();

	/// <summary>
	/// Which page each palette was left on, keyed by palette. Per palette rather than one shared
	/// number: switching to a preset and back should not lose your place in either.
	/// </summary>
	private static readonly Dictionary<string, int> pageByPalette = new();

	/// <summary>How many presets so far carry each name, while the palettes are being built.</summary>
	private static readonly Dictionary<string, int> nameUses = new();

	/// <summary>
	/// Every paint carried since entering the world. The auto palette keeps them all, so running out of
	/// one strikes it through in place instead of closing the ring up under your hand mid-build.
	/// </summary>
	private static readonly HashSet<int> carriedThisSession = new();

	/// <summary>
	/// All the automatic palette holds, before the setting that hides empty swatches thins it out for
	/// the wheel. What the palette board marks as in it.
	/// </summary>
	internal static readonly List<int> AutomaticPaints = new();

	internal static bool ScraperAvailable;
	internal static int PaletteIndex;
	internal static int PageIndex;

	/// <summary>
	/// Buttons on the bottom row: the coatings, then the mode buttons - bare placement and paint-both (when
	/// there is any paint to apply), then the scraper when one is carried. The layout only counts them, so a button
	/// costs nothing but an index.
	/// </summary>
	internal static int RowSlots => CoatingRow.Count + (NoPaintAvailable ? 2 : 0) + (ScraperAvailable ? 1 : 0);

	/// <summary>
	/// Refusing paint is only a choice when something would otherwise be applied. Carrying nothing means
	/// blocks already go down bare, and offering it then would also make an empty picker worth opening.
	/// </summary>
	private static bool NoPaintAvailable => Swatches.Count > 0 || CoatingRow.Count > 0;

	/// <summary>The circle that places blocks unpainted.</summary>
	internal static bool IsNoPaintButton(int index) => NoPaintAvailable && index == CoatingRow.Count;

	/// <summary>The circle that lets the brush and roller reach both halves of a tile - a painting switch, like bare placement.</summary>
	internal static bool IsPaintBothButton(int index) => NoPaintAvailable && index == CoatingRow.Count + 1;

	/// <summary>True when this bottom-row index is the scrape button rather than a coating.</summary>
	internal static bool IsScrapeButton(int index)
		=> ScraperAvailable && index == CoatingRow.Count + (NoPaintAvailable ? 2 : 0);

	internal static void RefreshStacks()
	{
		Player player = Main.LocalPlayer;

		SwatchStacks.Clear();
		foreach (int type in Swatches)
			SwatchStacks.Add(PaintInventory.TotalStack(player, type));
	}

	// ---- Palettes and pages -----------------------------------------------------------------

	/// <summary>Forgets every palette and page, for when the picker is reset.</summary>
	internal static void Clear()
	{
		ScraperAvailable = false;
		PaletteIndex = 0;
		PageIndex = 0;
		Palettes.Clear();
		Swatches.Clear();
		SwatchStacks.Clear();
		CoatingRow.Clear();
		pageByPalette.Clear();
		carriedThisSession.Clear();
	}

	/// <summary>Moves to the next or previous page. False when there is only the one page.</summary>
	internal static bool StepPage(int direction, PaintWheelConfig config)
	{
		int count = PageCount(config);
		if (count <= 1)
			return false;

		PageIndex = (PageIndex + direction + count) % count;
		ApplyPage(config);
		RememberPage();
		RefreshStacks();
		return true;
	}

	/// <summary>
	/// Makes a saved preset's palette the active one again, on the given page - for when its key has
	/// changed under it. Does nothing when no palette is that preset.
	/// </summary>
	internal static void FollowPreset(int preset, int page, PaintWheelConfig config)
	{
		for (int i = 0; i < Palettes.Count; i++) {
			if (Palettes[i].Preset != preset)
				continue;

			PaletteIndex = i;
			PageIndex = page;
			ApplyPage(config);
			RememberPage();
			return;
		}
	}

	/// <summary>
	/// Makes a palette the one in use by its preset - -1 for the automatic one - on the page it was left
	/// on. False when it has nothing to use, and so is not among the palettes the wheel offers.
	/// </summary>
	internal static bool SelectPreset(int preset, PaintWheelConfig config)
	{
		for (int i = 0; i < Palettes.Count; i++) {
			bool match = preset < 0 ? Palettes[i].Key == OwnedPaletteKey : Palettes[i].Preset == preset;
			if (match)
				return i == PaletteIndex || SelectPalette(i, config);
		}

		return false;
	}

	/// <summary>
	/// A palette's name as the player sees it: the automatic one's, or a saved one's with a number after
	/// it when an earlier one has the same name - the same rule the palette keys follow.
	/// </summary>
	internal static string PaletteName(int preset, PaintWheelConfig config)
	{
		if (preset < 0 || preset >= config.Presets.Count)
			return Language.GetTextValue("Mods.PaintWheel.UI.AutoPreset");

		(string name, int use) = NameAndUse(preset, config);
		return use == 1 ? name : $"{name} ({use})";
	}

	/// <summary>The key a saved palette is remembered by, as <see cref="RebuildPalettes"/> gives it out.</summary>
	internal static string PaletteKey(int preset, PaintWheelConfig config)
	{
		if (preset < 0 || preset >= config.Presets.Count)
			return OwnedPaletteKey;

		(string name, int use) = NameAndUse(preset, config);
		return KeyFor(name, use);
	}

	/// <summary>A palette's name, and which use of that name it is - 2 for the second palette called it.</summary>
	private static (string Name, int Use) NameAndUse(int preset, PaintWheelConfig config)
	{
		string name = NameOf(config.Presets[preset]);
		int use = 1;

		// Counted as the rebuild counts them, which skips a preset with no list at all.
		for (int i = 0; i < preset; i++) {
			if (config.Presets[i]?.Paints is not null && NameOf(config.Presets[i]) == name)
				use++;
		}

		return (name, use);
	}

	private static string KeyFor(string name, int use) => use == 1 ? name : $"{name}\u001F{use}";

	private static string NameOf(PaintPreset preset)
		=> string.IsNullOrWhiteSpace(preset?.Name) ? PaintPreset.DefaultName : preset.Name;

	/// <summary>Turns to the page holding the palette's <paramref name="index"/>th colour, so a keybind's step is on show.</summary>
	internal static void ShowPaletteIndex(int index, PaintWheelConfig config)
	{
		int perPage = PerPage(config);
		if (perPage <= 0)
			return;

		PageIndex = index / perPage;
		ApplyPage(config);
		RememberPage();
		RefreshStacks();
	}

	/// <summary>Switches to a palette, on the page it was left on. False for an index with no palette.</summary>
	internal static bool SelectPalette(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= Palettes.Count)
			return false;

		// Park the page we are leaving before moving, so coming back lands where it was.
		RememberPage();

		PaletteIndex = index;
		PageIndex = RememberedPage(Palettes[index]);

		ApplyPage(config);
		RememberPage();
		RefreshStacks();
		return true;
	}

	private static int PerPage(PaintWheelConfig config) => config.Layout switch {
		WheelLayoutStyle.Bar => Math.Clamp(config.Appearance.BarSwatchesPerPage, 1, 24),

		// Four rows of twelve. The grid exists so a full palette is visible at once, so its page is
		// sized to hold every paint the game has rather than to a taste setting.
		WheelLayoutStyle.Grid => WheelLayout.GridColumns * 4,

		_ => Math.Clamp(config.Appearance.MaxSwatches, 1, 12),
	};

	internal static Palette ActivePalette
		=> PaletteIndex >= 0 && PaletteIndex < Palettes.Count ? Palettes[PaletteIndex] : null;

	internal static int PageCount(PaintWheelConfig config)
	{
		Palette palette = ActivePalette;

		return palette is null ? 0 : WheelPaging.PageCount(palette.Paints.Count, PerPage(config));
	}

	internal static void RebuildPalettes(PaintWheelConfig config)
	{
		Palettes.Clear();

		// Auto-filled with everything you own, sorted by paint id - the order the Painter sells them in -
		// plus whatever you have carried since entering the world, the paint in use, and the paints put
		// in by hand on the palette board, less the ones taken out there.
		PaintInventory.CollectOwnedPaints(Main.LocalPlayer, paletteScratch);
		carriedThisSession.UnionWith(paletteScratch);

		foreach (int type in carriedThisSession) {
			if (!paletteScratch.Contains(type))
				paletteScratch.Add(type);
		}

		int chosen = PaintSelection.Paint;
		if (chosen > 0 && PaintCatalog.IsPaint(chosen) && !paletteScratch.Contains(chosen))
			paletteScratch.Add(chosen);

		if (PaintSelection.Local is PaintWheelPlayer state) {
			foreach (int type in state.AutoAdded) {
				if (PaintCatalog.IsPaint(type) && !paletteScratch.Contains(type))
					paletteScratch.Add(type);
			}

			// Unless that would empty it: this is the palette that is always there, and with nothing in
			// it the picker would stay shut, leaving no way back to the board to put a paint back.
			if (paletteScratch.Exists(type => !state.AutoRemoved.Contains(type)))
				paletteScratch.RemoveAll(state.AutoRemoved.Contains);
		}

		paletteScratch.Sort(PaintCatalog.ByPaintId);

		AutomaticPaints.Clear();
		AutomaticPaints.AddRange(paletteScratch);

		AddPalette(OwnedPaletteKey, Language.GetTextValue("Mods.PaintWheel.UI.AutoPreset"), paletteScratch, config);

		nameUses.Clear();

		for (int index = 0; index < config.Presets.Count; index++) {
			PaintPreset preset = config.Presets[index];
			if (preset?.Paints is null)
				continue;

			paletteScratch.Clear();
			foreach (var definition in preset.Paints) {
				if (definition is null || definition.IsUnloaded)
					continue;

				int type = definition.Type;

				// Kept even when not carried: a paint you are out of holds its place in the ring.
				if (type > 0 && PaintCatalog.IsPaint(type) && !paletteScratch.Contains(type))
					paletteScratch.Add(type);
			}

			// The key is the name, so it survives reordering and restarts. Two presets can share a name -
			// every one added with the config's '+' starts as "Palette" - so the second and later get a
			// key of their own; the first keeps the plain name that older saves remember. The separator is
			// a control character, which no name typed into the config can contain.
			string name = NameOf(preset);
			int use = nameUses[name] = nameUses.GetValueOrDefault(name) + 1;

			AddPalette(KeyFor(name, use), use == 1 ? name : $"{name} ({use})", paletteScratch, config, index);
		}

		ResolveActivePalette();
	}

	private static void AddPalette(string key, string name, List<int> types, PaintWheelConfig config,
		int preset = -1)
	{
		Player player = Main.LocalPlayer;

		scratch.Clear();
		foreach (int type in types) {
			if (config.Supply.HideEmptySwatches && PaintInventory.TotalStack(player, type) <= 0)
				continue;

			scratch.Add(type);
		}

		if (scratch.Count == 0)
			return;

		var palette = new Palette { Key = key, Name = name, Preset = preset };
		palette.Paints.AddRange(scratch);
		Palettes.Add(palette);
	}

	/// <summary>
	/// Restores the palette last chosen and its page. Matched on key rather than index, so adding or
	/// removing a preset cannot silently move the player somewhere else.
	/// </summary>
	private static void ResolveActivePalette()
	{
		PaletteIndex = 0;

		string wanted = PaintSelection.Palette;
		int found = IndexOfKey(wanted);

		// A palette left with the default name was keyed by it, and the default was "Preset" until
		// palettes were called palettes: the same palette is "Palette" now, so the old key finds it -
		// and is swapped for the new one, so the page it was left on still counts as its own.
		if (found < 0 && wanted == PaintPreset.LegacyDefaultName) {
			found = IndexOfKey(PaintPreset.DefaultName);
			if (found >= 0)
				PaintSelection.SetPalette(PaintPreset.DefaultName);
		}

		if (found >= 0)
			PaletteIndex = found;

		Palette palette = ActivePalette;
		PageIndex = palette is null ? 0 : RememberedPage(palette);
	}

	private static int IndexOfKey(string key)
	{
		if (string.IsNullOrEmpty(key))
			return -1;

		for (int i = 0; i < Palettes.Count; i++) {
			if (Palettes[i].Key == key)
				return i;
		}

		return -1;
	}

	/// <summary>Stores the page against its palette. ApplyPage has already clamped it to what fits.</summary>
	internal static void RememberPage()
	{
		Palette palette = ActivePalette;
		if (palette is null)
			return;

		pageByPalette[palette.Key] = PageIndex;

		// Written even when the palette list was never opened, so a restart still recognises the
		// auto-filled palette and restores its page.
		PaintSelection.SetPalette(palette.Key);
		PaintSelection.SetPage(PageIndex);
	}

	/// <summary>The page a palette was left on, falling back to what the character saved.</summary>
	private static int RememberedPage(Palette palette)
	{
		if (pageByPalette.TryGetValue(palette.Key, out int page))
			return page;

		return palette.Key == PaintSelection.Palette ? PaintSelection.Page : 0;
	}

	internal static void ApplyPage(PaintWheelConfig config)
	{
		Swatches.Clear();

		Palette palette = ActivePalette;
		if (palette is null)
			return;

		int pages = PageCount(config);
		PageIndex = pages <= 0 ? 0 : Math.Clamp(PageIndex, 0, pages - 1);

		WheelPaging.PageSlice(palette.Paints.Count, PerPage(config), PageIndex, out int start, out int count);

		for (int i = start; i < start + count; i++)
			Swatches.Add(palette.Paints[i]);
	}

	internal static void RebuildCoatingRow(PaintWheelConfig config)
	{
		CoatingRow.Clear();

		if (!config.Advanced.ShowCoatingRow)
			return;

		// Shown while one is on even when you carry none, so it can still be clicked off.
		if (!PaintInventory.OwnsAnyCoating(Main.LocalPlayer) && PaintSelection.Coating <= 0)
			return;

		// No "none" among them: the coating in use is clicked again to take it off.
		foreach (int type in PaintCatalog.Coatings) {
			if (CoatingRow.Count >= 4)
				break;

			CoatingRow.Add(type);
		}
	}
}
