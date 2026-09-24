using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// Building a palette in game: every paint laid out in a grid, click to add or remove. Also creating
/// and deleting saved palettes, which all end in writing the config.
/// </summary>
internal static class PaletteEditor
{
	/// <summary>Which config preset is being edited, or -1.</summary>
	private static int presetIndex = -1;

	/// <summary>The preset "+" just made, until its first edit ends, so one left empty can be thrown away.</summary>
	private static int newPreset = -1;

	private static string presetName;

	/// <summary>Every paint the game has, in catalog order: the cells of the grid.</summary>
	internal static readonly List<int> GridPaints = new();

	/// <summary>The paints in the palette, for asking "is this one in it".</summary>
	internal static readonly HashSet<int> Members = new();

	/// <summary>The same paints in the palette's own order, which is the order they sit in the ring.</summary>
	private static readonly List<int> order = new();

	internal static WheelLayout.GridSettings GridShape;

	/// <summary>True while a palette is being built rather than picked from.</summary>
	internal static bool Editing => presetIndex >= 0;

	/// <summary>Twelve columns, as the config grid uses: a hue and its Deep variant line up.</summary>
	private static void MeasureGrid()
	{
		GridShape = new WheelLayout.GridSettings {
			Count = GridPaints.Count,
			Columns = WheelLayout.GridColumns,
			Cell = 30f,
			Gap = 6f,
			Titled = true,
		};
	}

	internal static void StopEditing()
	{
		presetIndex = -1;
		newPreset = -1;
		presetName = null;
		GridPaints.Clear();
		Members.Clear();
		order.Clear();
	}

	internal static Vector2 GridCenter(in WheelLayout.Geometry geometry)
		=> WheelLayout.ClampGridCenter(geometry.MenuCenter, GridShape, Main.screenWidth, Main.screenHeight);

	/// <summary>
	/// Loads a saved palette into the grid: every paint the game has, with the palette's own marked.
	/// False when <paramref name="index"/> is not a saved palette.
	/// </summary>
	internal static bool Begin(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= config.Presets.Count)
			return false;

		presetIndex = index;
		presetName = config.Presets[index].Name;
		GridPaints.Clear();
		ReadMembers(config.Presets[index]);

		// Every paint the game has, in the same order the config grid uses - a palette is worth
		// building out of colours you have not bought yet.
		GridPaints.AddRange(PaintCatalog.Paints);
		MeasureGrid();

		return true;
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
		PaintPreset preset = config.Presets[presetIndex];
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

		foreach (ItemDefinition definition in preset.Paints ?? new List<ItemDefinition>()) {
			int type = definition?.IsUnloaded == false ? definition.Type : 0;
			if (type > 0 && PaintCatalog.IsPaint(type) && Members.Add(type))
				order.Add(type);
		}
	}

	private static readonly List<int> owned = new();

	/// <summary>
	/// Adds a palette of what you are carrying - an empty one when that is nothing, to fill in the
	/// editor - and makes it the one in use.
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

		if (saved)
			PaintSelection.SetPalette(preset.Name);

		// Rebuilt whether or not the save went through, so the rows always match the presets in memory.
		PickerContent.RebuildPalettes(config);
		PickerContent.ApplyPage(config);
		PaintPicker.BuildMenuRows();

		if (!saved || index >= config.Presets.Count)
			return -1;

		newPreset = index;
		return index;
	}

	/// <summary>Throws away the palette "+" made if its first edit ended with nothing in it.</summary>
	internal static void DiscardIfEmptyNew(PaintWheelConfig config)
	{
		int preset = newPreset;
		newPreset = -1;

		if (preset < 0 || preset != presetIndex || preset >= config.Presets.Count)
			return;

		if (config.Presets[preset].Paints?.Exists(definition => definition is { IsUnloaded: false }) == true)
			return;

		config.Presets.RemoveAt(preset);
		Save(config);
		PaintSelection.SetPalette(PickerContent.OwnedPaletteKey);
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

		config.Presets[preset].Name = name;
		Save(config);

		PickerContent.RebuildPalettes(config);

		if (shownPreset >= 0)
			PickerContent.FollowPreset(shownPreset, shownPage, config);

		PickerContent.ApplyPage(config);
		PaintPicker.BuildMenuRows();
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

		// Rebuilt whether or not the save went through, so the rows always match the presets in memory.
		PickerContent.RebuildPalettes(config);

		// Palettes sharing a name are keyed by their order, so taking out an earlier one renumbers the
		// ones after it. The one being shown is followed by its place in the list, not its old key.
		if (shownPreset > index)
			PickerContent.FollowPreset(shownPreset - 1, shownPage, config);

		PickerContent.ApplyPage(config);
		PaintPicker.BuildMenuRows();

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

	/// <summary>
	/// The palette being built: every paint laid out at once, the way the config grid shows them, so
	/// choosing is a matter of looking rather than paging. Members carry the gold rim.
	/// </summary>
	internal static void DrawGrid(SpriteBatch spriteBatch, in WheelLayout.Geometry geometry, float opacity)
	{
		Vector2 center = GridCenter(geometry);
		Rectangle bounds = WheelLayout.GridBounds(center, GridShape);

		WheelDrawing.DrawPanel(spriteBatch, bounds, opacity);
		DrawGridTitle(spriteBatch, center, opacity);

		Player player = Main.LocalPlayer;

		for (int i = 0; i < GridPaints.Count; i++) {
			int type = GridPaints[i];
			bool member = Members.Contains(type);
			bool hovered = i == PickerInput.HoveredCell;
			Rectangle cell = WheelLayout.GridCell(center, GridShape, i);
			Color accent = PaintCatalog.AccentColor(type);

			// A colour that is not in the palette is dimmed rather than merely unringed, so a full
			// palette does not read as a wall of gold with nothing to compare against.
			float strength = member ? 1f : hovered ? 0.72f : 0.42f;
			Color fill = Color.Lerp(new Color(38, 40, 58), accent, strength);

			// Its place in the palette - the order the ring deals them out in - in the corner, the way the
			// config's palette block numbers them, so the colour itself stays in view.
			string place = member ? (order.IndexOf(type) + 1).ToString() : null;
			WheelDrawing.DrawPaletteCell(spriteBatch, cell, fill, member, hovered, place, opacity);

			// Struck through when you are not carrying it: still worth adding, worth knowing you lack.
			if (PaintInventory.TotalStack(player, type) <= 0)
				WheelDrawing.DrawSlash(spriteBatch, cell.Center.ToVector2(), cell.Width * 0.66f, PickerRenderer.EmptyMark * opacity);
		}

		string hint = PickerInput.HoveredCell >= 0 && PickerInput.HoveredCell < GridPaints.Count
			? Lang.GetItemNameValue(GridPaints[PickerInput.HoveredCell])
			: Language.GetTextValue("Mods.PaintWheel.UI.EditHint");

		WheelDrawing.DrawTextCentered(spriteBatch, hint,
			new Vector2(bounds.Center.X, bounds.Bottom + 18f), Color.White, opacity, 0.85f);
	}

	private static void DrawGridTitle(SpriteBatch spriteBatch, Vector2 center, float opacity)
	{
		Rectangle band = WheelLayout.GridTitle(center, GridShape);

		// The palette being edited, which is not necessarily the one being used. Trimmed to leave room for
		// the tally and the Done button, so a long name cannot run under them.
		string tally = Members.Count.ToString();
		float room = band.Width - WheelLayout.GridActionWidth - WheelDrawing.MeasureText(tally, 0.74f).X - 24f;
		string title = WheelDrawing.Truncate(Language.GetTextValue("Mods.PaintWheel.UI.EditingHeader", presetName ?? ""), 0.8f, room);
		var at = new Vector2(band.X + 4f, band.Center.Y - 1f);

		WheelDrawing.DrawTextLeft(spriteBatch, title, at, Main.OurFavoriteColor, opacity, 0.8f);

		// Dimmed and set apart, so it reads as a tally rather than as part of the palette's name.
		WheelDrawing.DrawTextLeft(spriteBatch, tally,
			new Vector2(at.X + WheelDrawing.MeasureText(title, 0.8f).X + 9f, at.Y),
			new Color(150, 154, 184), opacity, 0.74f);

		Rectangle done = WheelLayout.GridTitleAction(center, GridShape);

		WheelDrawing.DrawRect(spriteBatch, done, (PickerInput.HoveredDone ? Color.White : PickerRenderer.RimColor) * (opacity * 0.22f));
		WheelDrawing.DrawRectOutline(spriteBatch, done, 1,
			(PickerInput.HoveredDone ? Color.White : new Color(120, 124, 156)) * opacity);

		WheelDrawing.DrawTextCentered(spriteBatch, Language.GetTextValue("Mods.PaintWheel.UI.DoneEditing"),
			new Vector2(done.Center.X, done.Center.Y - 1f),
			PickerInput.HoveredDone ? Color.White : new Color(214, 216, 234), opacity, 0.72f);

		WheelDrawing.DrawRect(spriteBatch, new Rectangle(band.X, band.Bottom - 1, band.Width, 1),
			PickerRenderer.RimColor * (opacity * 0.8f));
	}
}
