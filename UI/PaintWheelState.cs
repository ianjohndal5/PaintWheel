using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PaintWheel.Common;
using PaintWheel.Configs;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace PaintWheel.UI;

/// <summary>
/// The picker: open/close, palettes, paging, animation, hit testing and drawing.
/// <para/>
/// Palettes and pages are separate axes. A palette is a named set of paints, chosen from a list; a page
/// is one ring's worth of the palette you are on, walked with the arrows or the scroll wheel.
/// <para/>
/// All coordinates are UI space: UpdateUI and interface layers both run inside PlayerInput.SetZoom_UI,
/// which has already divided Main.mouseX/Y and screenWidth/Height by Main.UIScale.
/// </summary>
public static partial class PaintWheelState
{
	/// <summary>A palette: a named set of paints, shown one page at a time.</summary>
	private sealed class Source
	{
		/// <summary>Stable identity, for remembering the choice across sessions and languages.</summary>
		public string Key = "";

		public string Name = "";

		/// <summary>
		/// Which config preset this palette is, or -1 for the automatic one. Carried rather than looked
		/// up by name: two presets can share a name, and matching on it would edit or delete the wrong
		/// one. The key stays a name because it has to survive reordering and a restart.
		/// </summary>
		public int Preset = -1;

		public readonly List<int> Paints = new();
	}

	/// <summary>Which list, if any, is covering the swatches.</summary>
	private enum Overlay
	{
		None,
		Palettes,
		Scrape,

		/// <summary>Every paint at once, for building a palette.</summary>
		Grid,
	}

	private enum RowKind
	{
		Palette,
		ScrapeTarget,
		ExitScrape,
	}

	/// <summary>What activating a row did, which decides the sound and whether the picker stays up.</summary>
	private enum RowResult
	{
		Failed,
		Handled,
		CloseAfter,
	}

	private sealed class MenuRow
	{
		public RowKind Kind;
		public string Label = "";
		public int Index = -1;
		public ScrapeMode Mode;

		/// <summary>Index into the config presets, or -1 for a palette that cannot be edited.</summary>
		public int Preset = -1;
		public bool Current;
		public Source Preview;

		/// <summary>Shown dim after the name. 0 draws nothing.</summary>
		public int Count;
	}

	/// <summary>Key of the auto-filled palette. Prefixed so it cannot collide with a preset name.</summary>
	public const string OwnedPaletteKey = "*owned";

	private const int StickyGraceTicks = 10;

	/// <summary>How far the cursor must travel before a release counts as a choice rather than a cancel.</summary>
	private const float MovedThreshold = 7f;

	private static readonly List<Source> sources = new();
	private static readonly List<int> swatches = new();
	private static readonly List<int> swatchStacks = new();
	private static readonly List<int> coatingRow = new();
	private static readonly List<int> scratch = new();
	private static readonly List<int> sourceScratch = new();

	/// <summary>
	/// Which page each palette was left on, keyed by palette. Per palette rather than one shared
	/// number: switching to a preset and back should not lose your place in either.
	/// </summary>
	private static readonly Dictionary<string, int> pageByPalette = new();
	private static readonly List<MenuRow> menuRows = new();

	/// <summary>Shape of the open menu. Recomputed with the rows, not per frame - it only depends on them.</summary>
	private static WheelLayout.MenuSettings menuShape;

	private static bool active;
	private static bool viaKeybind;
	private static bool sticky;
	private static int framesOpen;
	private static float anim;
	private static Vector2 anchor;
	private static Vector2 openCursor;
	private static bool cursorMoved;
	private static Overlay overlay;

	/// <summary>
	/// What the drawing reads: tracks <see cref="overlay"/> while the picker is up, then holds still once
	/// it starts closing. A row that closes the picker usually changes the overlay too, and the fade
	/// takes a few frames, so drawing the live value flashes whatever was switched to.
	/// </summary>
	private static Overlay drawOverlay;

	private static bool scraperAvailable;
	private static bool leftHeldLast;
	private static bool rightHeldLast;
	private static bool freshLeftClick;
	private static bool freshRightClick;
	private static int hoveredSwatch = -1;
	private static int hoveredCoating = -1;
	private static int hoveredArrow = -1;
	private static int hoveredRow = -1;
	private static bool hoveredCenter;
	private static bool hoveredHeader;
	private static int sourceIndex;

	// Editing a palette: which config preset, and the paints it holds. Kept apart from the preset
	// itself so a half-finished edit is never what gets saved.
	private static int editIndex = -1;
	private static readonly List<int> editPaints = new();
	private static readonly HashSet<int> editMembers = new();
	private static string editName;
	private static WheelLayout.GridSettings gridShape;
	private static int hoveredCell = -1;
	private static int hoveredAction = -1;
	private static bool hoveredPlus;
	private static bool hoveredDone;
	private static int pageIndex;
	private static int openRequest;
	private static int openSuppression;

	/// <summary>True while the picker is accepting input.</summary>
	public static bool IsActive => active;

	/// <summary>
	/// Asks for the picker next frame. Deferred because the callers run during the player update, where
	/// mouse coordinates are in screen space - the anchor has to be sampled in UI space.
	/// </summary>
	public static void RequestOpen(bool viaKeybind)
	{
		// ProcessTriggers runs after UpdateUI, so without this the press that commits a stuck-open
		// picker would re-open it.
		if (active || openSuppression > 0)
			return;

		openRequest = viaKeybind ? 2 : 1;
	}

	/// <summary>
	/// What a picker that is not on screen must not still remember: what the cursor was over, and which
	/// list was open. Shared by the end of the close animation and by <see cref="Reset"/> so there is
	/// one list to keep up to date rather than two that drift.
	/// </summary>
	private static void ClearTransient()
	{
		StopEditing();
		hoveredCell = -1;
		hoveredAction = -1;
		hoveredPlus = false;
		hoveredDone = false;
		hoveredSwatch = -1;
		hoveredCoating = -1;
		hoveredArrow = -1;
		hoveredRow = -1;
		hoveredCenter = false;
		hoveredHeader = false;
		overlay = Overlay.None;
		drawOverlay = Overlay.None;
	}

	public static void Reset()
	{
		active = false;
		sticky = false;
		anim = 0f;
		openRequest = 0;
		openSuppression = 0;
		framesOpen = 0;
		cursorMoved = false;
		viaKeybind = false;
		anchor = Vector2.Zero;
		openCursor = Vector2.Zero;
		scraperAvailable = false;
		leftHeldLast = false;
		rightHeldLast = false;
		freshLeftClick = false;
		freshRightClick = false;
		ClearTransient();
		sourceIndex = 0;
		pageIndex = 0;
		sources.Clear();
		swatches.Clear();
		swatchStacks.Clear();
		coatingRow.Clear();
		menuRows.Clear();
		pageByPalette.Clear();
		WheelDrawing.ClearCaches();
	}

	// ---- Update -----------------------------------------------------------------------------

	public static void Update(GameTime gameTime)
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null) {
			Reset();
			return;
		}

		if (openRequest != 0) {
			bool keybind = openRequest == 2;
			openRequest = 0;
			if (!active)
				BeginOpen(keybind, config);
		}

		if (active) {
			if (ShouldCancel(config)) {
				Close(commit: false, config);
			}
			else {
				framesOpen++;
				Main.LocalPlayer.mouseInterface = true;

				// Sampled before this tick's clicks, so a closing click leaves the fade showing the list.
				drawOverlay = overlay;

				SampleButtons();
				scraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);
				RefreshStacks();
				UpdateHover(config);
				HandleClicks(config);
				HandleScroll(config);

				if (sticky) {
					// Dismissing is not choosing - in this mode clicking is what chooses.
					if (ShouldDismiss())
						Close(commit: false, config);
				}
				else if (ShouldCommit(config)) {
					Close(commit: true, config);
				}
			}
		}

		float step = 1f / Math.Max(1, config.Appearance.OpenAnimationTicks);
		anim = MathHelper.Clamp(anim + (active ? step : -step * 1.6f), 0f, 1f);

		if (!active && anim <= 0f)
			ClearTransient();

		if (openSuppression > 0)
			openSuppression--;
	}

	private static void BeginOpen(bool keybind, PaintWheelConfig config)
	{
		// Checked first: opening into a state that cancels on the same tick flashes the picker.
		if (EnvironmentBlocked(config))
			return;

		scraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);

		// Scrape mode is sticky, but it cannot survive losing the scraper.
		if (PaintSelection.Scrape != ScrapeMode.Off && !scraperAvailable)
			PaintSelection.ExitScrape();

		bool scraping = PaintSelection.Scrape != ScrapeMode.Off;

		RebuildSources(config);
		RebuildCoatingRow(config);
		ApplyPage(config);

		// Scrape mode and a carried scraper both count, or a player with no paint could never reach the
		// list that enters scrape mode.
		if (!scraping && !HasAnythingToShow)
			return;

		openCursor = Main.MouseScreen;
		cursorMoved = false;
		overlay = scraping ? Overlay.Scrape : Overlay.None;
		drawOverlay = overlay;
		BuildMenuRows();
		anchor = WheelLayout.ClampAnchor(BuildSettings(config, 1f), openCursor, Main.screenWidth, Main.screenHeight);

		active = true;
		viaKeybind = keybind;

		// Click mode is up from the start; hold mode can still stick later if the keybind was tapped.
		sticky = !keybind && config.OpenWithRightClick && config.OpenMode == PickerOpenMode.Click;
		framesOpen = 0;

		// A button already down when the picker opens is not a click on it.
		leftHeldLast = Main.mouseLeft;
		rightHeldLast = Main.mouseRight;
		freshLeftClick = false;
		freshRightClick = false;
		hoveredSwatch = -1;
		hoveredCoating = -1;
		hoveredArrow = -1;
		hoveredRow = -1;
		hoveredCenter = false;
		hoveredHeader = false;

		RefreshStacks();
		RememberPage();
		Play(SoundID.MenuOpen, config);
	}

	private static void Close(bool commit, PaintWheelConfig config)
	{
		bool selected = false;

		if (commit && cursorMoved) {
			if (overlay != Overlay.None) {
				if (hoveredRow >= 0)
					selected = ActivateRow(hoveredRow, config) != RowResult.Failed;
			}
			else if (IsScrapeButton(hoveredCoating)) {
				// Entered, but its list is not opened: the picker is closing, and a menu that appears
				// for the length of the fade and vanishes reads as a glitch. It is there next time.
				selected = PaintSelection.EnterScrape();
			}
			else if (IsNoPaintButton(hoveredCoating)) {
				PaintSelection.ToggleNoPaint();
				selected = true;
			}
			else if (hoveredCoating >= 0 && hoveredCoating < coatingRow.Count) {
				PaintSelection.SelectCoating(coatingRow[hoveredCoating]);
				selected = true;
			}
			else if (hoveredSwatch >= 0 && hoveredSwatch < swatches.Count) {
				PaintSelection.SelectPaint(swatches[hoveredSwatch]);
				selected = true;
			}
		}

		Play(selected ? SoundID.Grab : SoundID.MenuClose, config);

		CloseSilently();
	}

	/// <summary>Puts the picker away without a sound, for when the caller has already made one.</summary>
	private static void CloseSilently()
	{
		active = false;
		sticky = false;
		overlay = Overlay.None;
		framesOpen = 0;
		openSuppression = 2;
	}

	private static void OpenOverlay(Overlay which, PaintWheelConfig config)
	{
		// Synced on the spot: only a close freezes the picture, opening a list should show it now.
		overlay = which;
		drawOverlay = which;
		BuildMenuRows();

		hoveredSwatch = -1;
		hoveredCoating = -1;
		hoveredCenter = false;
		hoveredHeader = false;

		ResetMenuCursor();
		Play(SoundID.MenuOpen, config);
	}

	/// <summary>Whether the centre and the header have a list worth opening.</summary>
	/// <summary>
	/// Worth opening when there is somewhere to switch to, and also when there is not: the list is
	/// where a palette gets made. Still false with no paints at all, so an empty picker stays shut.
	/// </summary>
	private static bool MenuAvailable => Editing || sources.Count > 1 || swatches.Count > 0;

	/// <summary>
	/// Discs on the bottom row: the coatings, then the scrape button when a scraper is carried. The
	/// layout only counts them, so the button costs nothing but an index.
	/// </summary>
	// Coatings first, then the mode buttons: bare placement, then the scraper if one is carried.
	private static int RowSlots => coatingRow.Count + (NoPaintAvailable ? 1 : 0) + (scraperAvailable ? 1 : 0);

	/// <summary>
	/// Refusing paint is only a choice when something would otherwise be applied. Carrying nothing means
	/// blocks already go down bare, and offering it then would also make an empty picker worth opening.
	/// </summary>
	private static bool NoPaintAvailable => swatches.Count > 0 || coatingRow.Count > 0;

	/// <summary>The circle that places blocks unpainted.</summary>
	private static bool IsNoPaintButton(int index) => NoPaintAvailable && index == coatingRow.Count;

	/// <summary>True when this bottom-row index is the scrape button rather than a coating.</summary>
	private static bool IsScrapeButton(int index)
		=> scraperAvailable && index == coatingRow.Count + (NoPaintAvailable ? 1 : 0);

	/// <summary>
	/// The rows of whichever list is open - one list, so hit testing, drawing and activation cannot
	/// disagree about what row three is.
	/// </summary>
	private static void BuildMenuRows()
	{
		BuildMenuRowList();
		MeasureMenu();
	}

	private static void BuildMenuRowList()
	{
		menuRows.Clear();

		if (overlay == Overlay.Palettes) {
			// While editing, the list is the way out rather than a way to somewhere else: switching
			// palettes mid-edit would leave you editing one you can no longer see.
			PaintWheelConfig config = PaintWheelConfig.Instance;

			for (int i = 0; i < sources.Count; i++) {
				menuRows.Add(new MenuRow {
					Kind = RowKind.Palette,
					Index = i,
					Label = sources[i].Name,
					Current = i == sourceIndex,
					Preview = sources[i],
					Count = sources[i].Paints.Count,

					// Only a saved palette can be edited or thrown away; the automatic one is neither.
					Preset = sources[i].Preset,
				});
			}

			return;
		}

		if (overlay != Overlay.Scrape)
			return;

		ScrapeMode current = PaintSelection.Scrape;

		AddScrapeRow(ScrapeMode.BlocksAndWalls, "BlocksAndWalls", current);
		AddScrapeRow(ScrapeMode.BlocksOnly, "BlocksOnly", current);
		AddScrapeRow(ScrapeMode.WallsOnly, "WallsOnly", current);

		menuRows.Add(new MenuRow {
			Kind = RowKind.ExitScrape,
			Label = Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Exit"),
		});
	}

	private static void AddScrapeRow(ScrapeMode mode, string key, ScrapeMode current)
	{
		menuRows.Add(new MenuRow {
			Kind = RowKind.ScrapeTarget,
			Mode = mode,
			Label = Language.GetTextValue("Mods.PaintWheel.UI.Scrape." + key),
			Current = mode == current,
		});
	}

	private static RowResult ActivateRow(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= menuRows.Count)
			return RowResult.Failed;

		MenuRow row = menuRows[index];

		switch (row.Kind) {
			case RowKind.Palette:
				SetSource(row.Index, config);
				overlay = Overlay.None;
				return RowResult.Handled;

			case RowKind.ScrapeTarget:
				PaintSelection.SetScrapeTarget(row.Mode);

				// Rebuilt so the mark moves to the target just chosen. Without this the rows keep the
				// state they were built with, and a picker that stays open shows the old one - which
				// reads as the click having done nothing.
				BuildMenuRows();
				return RowResult.CloseAfter;

			case RowKind.ExitScrape:
				PaintSelection.ExitScrape();
				overlay = Overlay.None;
				return RowResult.CloseAfter;

			default:
				return RowResult.Failed;
		}
	}

	/// <summary>Switches to scrape mode and shows its list, leaving the picker up.</summary>
	private static bool EnterScrapeFromRow(PaintWheelConfig config)
	{
		if (!PaintSelection.EnterScrape())
			return false;

		overlay = Overlay.Scrape;
		drawOverlay = Overlay.Scrape;
		BuildMenuRows();
		ResetMenuCursor();

		hoveredCoating = -1;
		return true;
	}

	private static void ChangePage(int direction, PaintWheelConfig config)
	{
		int count = PageCount(config);
		if (count <= 1)
			return;

		pageIndex = (pageIndex + direction + count) % count;
		ApplyPage(config);
		RememberPage();
		RefreshStacks();
		hoveredSwatch = -1;
		Play(SoundID.MenuTick, config);
	}

	/// <summary>
	/// Steps palette for a keybind, whether or not the picker is up. The list is built here when it is
	/// not, since a keybind can be pressed before the picker has ever been opened; rebuilding also
	/// re-resolves the current palette from the one saved on the character.
	/// </summary>
	/// <returns>The palette now active, or null when there is nothing to step through.</returns>
	public static string CyclePaletteExternally(int direction)
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null)
			return null;

		if (!active) {
			scraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);
			RebuildSources(config);
		}

		if (sources.Count <= 1)
			return null;

		CyclePalette(direction, config);

		// The open picker's rows carry the mark for the current palette, so they go stale otherwise.
		if (overlay == Overlay.Palettes)
			BuildMenuRows();

		return ActiveSource?.Name;
	}

	/// <summary>
	/// Enters or leaves scrape mode for a keybind, whether or not the picker is up. An open picker is
	/// moved onto the matching view, so the key and the wheel can never disagree about the mode.
	/// </summary>
	/// <returns>The mode now in force, or null when there is no scraper to pick up.</returns>
	public static ScrapeMode? ToggleScrapeExternally()
	{
		if (PaintSelection.Scrape != ScrapeMode.Off) {
			PaintSelection.ExitScrape();

			if (active) {
				overlay = Overlay.None;
				drawOverlay = Overlay.None;
				hoveredRow = -1;
			}

			return ScrapeMode.Off;
		}

		if (!PaintSelection.EnterScrape())
			return null;

		if (active) {
			overlay = Overlay.Scrape;
			drawOverlay = Overlay.Scrape;
			BuildMenuRows();
			ResetMenuCursor();
			hoveredCoating = -1;
		}

		return PaintSelection.Scrape;
	}

	private static void CyclePalette(int direction, PaintWheelConfig config)
	{
		if (sources.Count <= 1)
			return;

		SetSource((sourceIndex + direction + sources.Count) % sources.Count, config);
		Play(SoundID.MenuTick, config);
	}

	private static void SetSource(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= sources.Count)
			return;

		// Park the page we are leaving before moving, so coming back lands where it was.
		RememberPage();

		sourceIndex = index;
		pageIndex = RememberedPage(sources[index]);

		ApplyPage(config);
		RememberPage();
		RefreshStacks();
		hoveredSwatch = -1;
	}

	private static void RefreshStacks()
	{
		Player player = Main.LocalPlayer;

		swatchStacks.Clear();
		foreach (int type in swatches)
			swatchStacks.Add(PaintInventory.TotalStack(player, type));
	}

	// ---- Palettes and pages -----------------------------------------------------------------

	private static int PerPage(PaintWheelConfig config) => config.Layout switch {
		WheelLayoutStyle.Bar => Math.Clamp(config.Appearance.BarSwatchesPerPage, 1, 24),

		// Four rows of twelve. The grid exists so a full palette is visible at once, so its page is
		// sized to hold every paint the game has rather than to a taste setting.
		WheelLayoutStyle.Grid => WheelLayout.GridColumns * 4,

		_ => Math.Clamp(config.Appearance.MaxSwatches, 1, 12),
	};

	private static Source ActiveSource
		=> sourceIndex >= 0 && sourceIndex < sources.Count ? sources[sourceIndex] : null;

	/// <summary>True while a palette is being built rather than picked from.</summary>
	private static bool Editing => editIndex >= 0;



	/// <summary>Twelve columns, as the config grid uses: a hue and its Deep variant line up.</summary>
	private static void MeasureGrid()
	{
		gridShape = new WheelLayout.GridSettings {
			Count = editPaints.Count,
			Columns = WheelLayout.GridColumns,
			Cell = 30f,
			Gap = 6f,
			Titled = true,
		};
	}

	private static void StopEditing()
	{
		editIndex = -1;
		editName = null;
		editPaints.Clear();
		editMembers.Clear();
	}

	private static int PageCount(PaintWheelConfig config)
	{
		Source source = ActiveSource;

		return source is null ? 0 : WheelPaging.PageCount(source.Paints.Count, PerPage(config));
	}

	private static void RebuildSources(PaintWheelConfig config)
	{
		sources.Clear();

		// Auto-filled with everything you own, sorted by paint id - the order the Painter sells them in.
		PaintInventory.CollectOwnedPaints(Main.LocalPlayer, sourceScratch);

		int chosen = PaintSelection.Paint;
		if (chosen > 0 && PaintCatalog.IsPaint(chosen) && !sourceScratch.Contains(chosen)) {
			sourceScratch.Add(chosen);
			sourceScratch.Sort(static (a, b) => PaintCatalog.PaintIdOf(a).CompareTo(PaintCatalog.PaintIdOf(b)));
		}

		AddSource(OwnedPaletteKey, Language.GetTextValue("Mods.PaintWheel.UI.AutoPreset"), sourceScratch, config);

		for (int index = 0; index < config.Presets.Count; index++) {
			PaintPreset preset = config.Presets[index];
			if (preset?.Paints is null)
				continue;

			sourceScratch.Clear();
			foreach (var definition in preset.Paints) {
				if (definition is null || definition.IsUnloaded)
					continue;

				int type = definition.Type;

				// Kept even when not carried: a paint you are out of holds its place in the ring.
				if (type > 0 && PaintCatalog.IsPaint(type) && !sourceScratch.Contains(type))
					sourceScratch.Add(type);
			}

			string name = string.IsNullOrWhiteSpace(preset.Name) ? "Preset" : preset.Name;
			AddSource(name, name, sourceScratch, config, index);
		}

		ResolveSource();
	}

	private static void AddSource(string key, string name, List<int> types, PaintWheelConfig config,
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

		var source = new Source { Key = key, Name = name, Preset = preset };
		source.Paints.AddRange(scratch);
		sources.Add(source);
	}

	/// <summary>
	/// Restores the palette last chosen and its page. Matched on key rather than index, so adding or
	/// removing a preset cannot silently move the player somewhere else.
	/// </summary>
	private static void ResolveSource()
	{
		sourceIndex = 0;

		string wanted = PaintSelection.Palette;
		if (!string.IsNullOrEmpty(wanted)) {
			for (int i = 0; i < sources.Count; i++) {
				if (sources[i].Key == wanted) {
					sourceIndex = i;
					break;
				}
			}
		}

		Source source = ActiveSource;
		pageIndex = source is null ? 0 : RememberedPage(source);
	}

	/// <summary>Stores the page against its palette. ApplyPage has already clamped it to what fits.</summary>
	private static void RememberPage()
	{
		Source source = ActiveSource;
		if (source is null)
			return;

		pageByPalette[source.Key] = pageIndex;

		// Written even when the palette list was never opened, so a restart still recognises the
		// auto-filled palette and restores its page.
		PaintSelection.SetPalette(source.Key);
		PaintSelection.SetPage(pageIndex);
	}

	/// <summary>The page a palette was left on, falling back to what the character saved.</summary>
	private static int RememberedPage(Source source)
	{
		if (pageByPalette.TryGetValue(source.Key, out int page))
			return page;

		return source.Key == PaintSelection.Palette ? PaintSelection.Page : 0;
	}

	private static void ApplyPage(PaintWheelConfig config)
	{
		swatches.Clear();

		Source source = ActiveSource;
		if (source is null)
			return;

		int pages = PageCount(config);
		pageIndex = pages <= 0 ? 0 : Math.Clamp(pageIndex, 0, pages - 1);

		WheelPaging.PageSlice(source.Paints.Count, PerPage(config), pageIndex, out int start, out int count);

		for (int i = start; i < start + count; i++)
			swatches.Add(source.Paints[i]);
	}

	private static void RebuildCoatingRow(PaintWheelConfig config)
	{
		coatingRow.Clear();

		if (!config.Advanced.ShowCoatingRow)
			return;

		if (!PaintInventory.OwnsAnyCoating(Main.LocalPlayer) && PaintSelection.Coating <= 0)
			return;

		coatingRow.Add(0);
		foreach (int type in PaintCatalog.Coatings) {
			if (coatingRow.Count >= 5)
				break;

			coatingRow.Add(type);
		}
	}

	private static WheelLayout.Settings BuildSettings(PaintWheelConfig config, float progress) => new() {
		Style = config.Layout,
		Count = swatches.Count,
		CoatingCount = RowSlots,
		ShowHeader = drawOverlay == Overlay.Scrape || MenuAvailable || PageCount(config) > 1,
		Radius = config.Appearance.WheelRadius,
		Swatch = config.Appearance.SwatchSize,
		DeadZone = config.Appearance.DeadZoneRadius,
		CellWidth = config.Appearance.BarCellWidth,
		CellHeight = config.Appearance.BarCellHeight,
		LabelWidth = WheelDrawing.MeasureText(HeaderLabel(config), HeaderTextScale).X,
		Progress = progress,
	};

	private const float HeaderTextScale = 0.85f;

	/// <summary>The palette name, plus the page count when there is more than one page.</summary>
	private static string HeaderLabel(PaintWheelConfig config)
	{
		if (drawOverlay == Overlay.Scrape)
			return Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Header");

		Source source = ActiveSource;
		if (source is null)
			return null;

		if (Editing) {
			return Language.GetTextValue("Mods.PaintWheel.UI.EditingHeader", source.Name, editMembers.Count);
		}

		int pages = PageCount(config);

		return pages > 1 ? $"{source.Name}  {pageIndex + 1}/{pages}" : source.Name;
	}

	private const float RowTextScale = 0.82f;
	private const float RowCountScale = 0.72f;
	private const int RowPreviewDots = 6;
	private const float RowLeftPad = 10f;
	private const float RowRightPad = 10f;
	private const float RowCountGap = 6f;

	/// <summary>Measured from the rows, so a long preset name widens the panel instead of overflowing it.</summary>
	private static void MeasureMenu()
	{
		float widest = 0f;

		foreach (MenuRow row in menuRows)
			widest = MathF.Max(widest, RowWidthNeeded(row));

		// Measured with the same arithmetic the drawing uses to work out the room left for a name, so
		// the widest row fits exactly instead of being trimmed by its own margin.
		menuShape = new WheelLayout.MenuSettings {
			Rows = menuRows.Count,
			Width = widest + WheelLayout.MenuPadding * 2f,
			Titled = true,
		};
	}

	/// <summary>
	/// Room kept at the right of a row for the preview dots, or the chevron. Shared by measuring and
	/// drawing so a name can be trimmed to exactly what is left.
	/// </summary>
	/// <summary>
	/// Row width this entry needs: pads, name, count, and the room kept at the right. Plus a pixel,
	/// because the panel and row edges are each rounded and an exact fit can round the wrong way.
	/// </summary>
	private static float RowWidthNeeded(MenuRow row)
		=> RowLeftPad + WheelDrawing.MeasureText(row.Label, RowTextScale).X + CountWidth(row)
			+ TrailingRoom(row.Preview is not null) + ActionRoom(row) + RowRightPad + 2f;

	/// <summary>Two buttons on a saved palette - open it for editing, or throw it away.</summary>
	private static int ActionCount(MenuRow row) => row.Preset >= 0 ? 2 : 0;

	private static float ActionRoom(MenuRow row) => WheelLayout.MenuActionRoom(ActionCount(row));

	/// <summary>Room left for the name once the count and the trailing space are taken out.</summary>
	private static float NameRoom(Rectangle row, MenuRow entry)
		=> row.Width - RowLeftPad - RowRightPad - TrailingRoom(entry.Preview is not null)
			- CountWidth(entry) - ActionRoom(entry);

	private static float CountWidth(MenuRow row)
		=> row.Count > 0 ? RowCountGap + WheelDrawing.MeasureText(row.Count.ToString(), RowCountScale).X : 0f;

	private static float TrailingRoom(bool previews)
		=> previews ? RowPreviewDots * (WheelLayout.MenuRowHeight * 0.42f + 3f) + 10f : 22f;

	private static Vector2 MenuCenter(in WheelLayout.Geometry geometry)
		=> WheelLayout.ClampMenuCenter(geometry.MenuCenter, menuShape, Main.screenWidth, Main.screenHeight);

	private static Vector2 GridCenter(in WheelLayout.Geometry geometry)
		=> WheelLayout.ClampGridCenter(geometry.MenuCenter, gridShape, Main.screenWidth, Main.screenHeight);

	// ---- Editing a palette ------------------------------------------------------------------

	/// <summary>
	/// Starts editing the preset behind the active palette. The swatches become everything you own
	/// plus whatever the palette already holds, so a colour you have run out of can still be removed.
	/// </summary>
	private static bool StartEditing(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= config.Presets.Count)
			return false;

		editIndex = index;
		editName = config.Presets[index].Name;
		editMembers.Clear();
		editPaints.Clear();

		foreach (ItemDefinition definition in config.Presets[index].Paints ?? new List<ItemDefinition>()) {
			int type = definition?.IsUnloaded == false ? definition.Type : 0;
			if (type > 0 && PaintCatalog.IsPaint(type))
				editMembers.Add(type);
		}

		// Every paint the game has, in the same order the config grid uses - a palette is worth
		// building out of colours you have not bought yet.
		editPaints.AddRange(PaintCatalog.Paints);

		// Clicking is what edits, so the picker has to stay up even in hold mode.
		sticky = true;
		overlay = Overlay.Grid;
		drawOverlay = Overlay.Grid;
		MeasureGrid();
		ResetMenuCursor();

		return true;
	}

	/// <summary>
	/// Leaves the grid for the list it was opened from, rather than putting the picker away: the
	/// palette you have just built is usually the one you then want to use.
	/// </summary>
	private static void FinishEditing(PaintWheelConfig config)
	{
		StopEditing();
		RebuildSources(config);
		ApplyPage(config);

		overlay = Overlay.Palettes;
		drawOverlay = Overlay.Palettes;
		BuildMenuRows();
		ResetMenuCursor();
	}

	/// <summary>Adds or removes one paint, and writes it straight to the config.</summary>
	private static void ToggleMember(int type, PaintWheelConfig config)
	{
		if (!Editing || type <= 0)
			return;

		if (!editMembers.Remove(type))
			editMembers.Add(type);

		Commit(config);
	}

	/// <summary>Builds a palette out of what you are carrying, and switches to it.</summary>
	private static bool CreatePreset(PaintWheelConfig config)
	{
		PaintInventory.CollectOwnedPaints(Main.LocalPlayer, sourceScratch);
		if (sourceScratch.Count == 0)
			return false;

		var preset = new PaintPreset { Name = NextPresetName(config) };
		foreach (int type in sourceScratch)
			preset.Paints.Add(new ItemDefinition(type));

		config.Presets.Add(preset);
		if (!Save(config))
			return false;

		PaintSelection.SetPalette(preset.Name);
		RebuildSources(config);
		ApplyPage(config);
		BuildMenuRows();

		return true;
	}

	private static bool DeletePreset(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= config.Presets.Count)
			return false;

		config.Presets.RemoveAt(index);
		StopEditing();

		if (!Save(config))
			return false;

		// Back to the palette that is always there, since the one being shown has just gone.
		PaintSelection.SetPalette(OwnedPaletteKey);
		RebuildSources(config);
		ApplyPage(config);
		BuildMenuRows();

		return true;
	}

	/// <summary>Writes the edited membership into the preset, in paint id order.</summary>
	private static void Commit(PaintWheelConfig config)
	{
		if (editIndex < 0 || editIndex >= config.Presets.Count)
			return;

		PaintPreset preset = config.Presets[editIndex];
		preset.Paints ??= new List<ItemDefinition>();
		preset.Paints.Clear();

		foreach (int type in editPaints) {
			if (editMembers.Contains(type))
				preset.Paints.Add(new ItemDefinition(type));
		}

		Save(config);
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

	// ---- Sound ------------------------------------------------------------------------------

	private static void Play(SoundStyle style, PaintWheelConfig config)
	{
		if (config.PlaySounds)
			SoundEngine.PlaySound(style);
	}

	/// <summary>Used by the eyedropper and quick-toggle keybinds so they feel like a commit.</summary>
	public static void PlayCommitSound()
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null || config.PlaySounds)
			SoundEngine.PlaySound(SoundID.Grab);
	}
}
