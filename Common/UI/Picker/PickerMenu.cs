using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using ReLogic.Localization.IME;
using ReLogic.OS;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;

namespace PaintWheel.Common.UI.Picker;

/// <summary>What a row in one of the lists does when chosen.</summary>
internal enum RowKind
{
	/// <summary>Switches to a palette.</summary>
	Palette,

	/// <summary>Sets what the scraper may strip.</summary>
	ScrapeTarget,

	/// <summary>Leaves scrape mode, back to the colours.</summary>
	ExitScrape,
}

/// <summary>What activating a row did, which decides the sound and whether the picker stays up.</summary>
internal enum RowResult
{
	Failed,
	Handled,
	CloseAfter,
}

/// <summary>One row of a list, built fresh whenever what it shows changes.</summary>
internal sealed class MenuRow
{
	public RowKind Kind;
	public string Label = "";

	/// <summary>Which palette a palette row switches to.</summary>
	public int Index = -1;

	/// <summary>Which target a scrape row sets.</summary>
	public ScrapeMode Mode;

	/// <summary>Index into the config presets, or -1 for a palette that cannot be edited.</summary>
	public int Preset = -1;
	public bool Current;
	public Palette Preview;

	/// <summary>Shown dim after the name. 0 draws nothing.</summary>
	public int Count;
}

/// <summary>
/// The lists that cover the swatches - palettes and scrape targets. One list of rows, so hit testing,
/// drawing and activation cannot disagree about what row three is.
/// </summary>
internal static class PickerMenu
{
	internal static readonly List<MenuRow> Rows = new();

	/// <summary>Shape of the open menu. Recomputed with the rows, not per frame - it only depends on them.</summary>
	internal static WheelLayout.MenuSettings Shape;

	/// <summary>Builds the rows of the list <paramref name="which"/>, and sizes the panel to fit them.</summary>
	internal static void Build(PickerOverlay which)
	{
		// A list just opened scrolls to the marked row; one rebuilt under the player - a rename, a
		// delete, a "+" - stays where it was scrolled to, so the next click lands on what it aimed at.
		bool opened = which != builtFor;
		builtFor = which;

		BuildRows(which);
		MeasureMenu(opened);
	}

	/// <summary>The list the rows were last built for.</summary>
	private static PickerOverlay builtFor = PickerOverlay.None;

	/// <summary>The marked row when the rows were last built.</summary>
	private static int lastCurrent = -1;

	/// <summary>The next list shown counts as just opened: the picker was put away, or a list was left.</summary>
	internal static void ForgetOpenList() => builtFor = PickerOverlay.None;

	internal static void Clear()
	{
		Rows.Clear();
		FirstShown = 0;
		builtFor = PickerOverlay.None;
		StopRenaming();
	}

	private static void BuildRows(PickerOverlay which)
	{
		Rows.Clear();

		if (which == PickerOverlay.Palettes) {
			for (int i = 0; i < PickerContent.Palettes.Count; i++) {
				Rows.Add(new MenuRow {
					Kind = RowKind.Palette,
					Index = i,
					Label = PickerContent.Palettes[i].Name,
					Current = i == PickerContent.PaletteIndex,
					Preview = PickerContent.Palettes[i],
					Count = PickerContent.Palettes[i].Paints.Count,

					// Only a saved palette can be edited or thrown away; the automatic one is neither.
					Preset = PickerContent.Palettes[i].Preset,
				});
			}

			return;
		}

		if (which != PickerOverlay.Scrape)
			return;

		ScrapeMode current = PaintSelection.Scrape;

		AddScrapeRow(ScrapeMode.BlocksAndWalls, "BlocksAndWalls", current);
		AddScrapeRow(ScrapeMode.BlocksOnly, "BlocksOnly", current);
		AddScrapeRow(ScrapeMode.WallsOnly, "WallsOnly", current);
		AddScrapeRow(ScrapeMode.CoatingsOnly, "CoatingsOnly", current);

		Rows.Add(new MenuRow {
			Kind = RowKind.ExitScrape,
			Label = Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Exit"),
		});
	}

	private static void AddScrapeRow(ScrapeMode mode, string key, ScrapeMode current)
	{
		Rows.Add(new MenuRow {
			Kind = RowKind.ScrapeTarget,
			Mode = mode,
			Label = Language.GetTextValue("Mods.PaintWheel.UI.Scrape." + key),
			Current = mode == current,
		});
	}

	private const float RowTextScale = 0.82f;
	private const float RowCountScale = 0.72f;
	private const int RowPreviewDots = 6;
	private const float RowLeftPad = 10f;
	private const float RowRightPad = 10f;
	private const float RowCountGap = 6f;

	/// <summary>Room for the number key's digit at the left of a row, when those are shown.</summary>
	private const float DigitRoom = 12f;

	/// <summary>
	/// How many rows show at once. More than this scroll, so a long list of palettes stays on screen;
	/// eight is as many as fit a small screen at a large UI scale.
	/// </summary>
	internal const int MaxShownRows = 8;

	/// <summary>The row at the top of the list, when there are more rows than fit.</summary>
	internal static int FirstShown;

	internal static int ShownRows => Math.Min(Rows.Count, MaxShownRows);

	// The buttons on a saved palette's row, left to right.
	internal const int ActionEdit = 0;
	internal const int ActionRename = 1;
	internal const int ActionDelete = 2;

	/// <summary>The saved palette whose name is being typed, or -1.</summary>
	internal static int Renaming = -1;

	private static string renameText = "";

	/// <summary>Longest name the field takes. The config's own text box has no limit, so one set there may be longer.</summary>
	private const int MaxNameLength = 40;

	internal static bool IsRenaming => Renaming >= 0;

	/// <summary>
	/// Keeps vanilla's chat off the keyboard while a name is typed - Enter would open it, and opening
	/// it clears the input before the Enter that keeps the name is read. The game clears the claim
	/// every update, before the UI updates, so it is renewed from there.
	/// </summary>
	internal static void ClaimTextInput()
	{
		if (IsRenaming)
			Main.CurrentInputTextTakerOverride = typeof(PickerMenu);
	}

	/// <summary>Measured from the rows, so a long palette name widens the panel instead of overflowing it.</summary>
	private static void MeasureMenu(bool opened)
	{
		float widest = 0f;

		foreach (MenuRow row in Rows)
			widest = MathF.Max(widest, RowWidthNeeded(row));

		// Measured with the same arithmetic the drawing uses to work out the room left for a name, so
		// the widest row fits exactly instead of being trimmed by its own margin.
		Shape = new WheelLayout.MenuSettings {
			Rows = ShownRows,
			Width = widest + WheelLayout.MenuPadding * 2f,
			Titled = true,
		};

		// Brings the marked row into view when the list opens, or when the mark moves - a palette key
		// pressed with the list up - but not for a rebuild that leaves it where it was.
		int current = Rows.FindIndex(row => row.Current);
		bool follow = opened || current != lastCurrent;
		lastCurrent = current;

		FirstShown = Math.Clamp(FirstShown, 0, Math.Max(0, Rows.Count - ShownRows));

		if (follow && current >= 0 && (current < FirstShown || current >= FirstShown + ShownRows))
			FirstShown = Math.Clamp(current - ShownRows / 2, 0, Math.Max(0, Rows.Count - ShownRows));
	}

	/// <summary>Moves the list by one row. False when it is already at that end, or everything fits.</summary>
	internal static bool Scroll(int direction)
	{
		int next = Math.Clamp(FirstShown + direction, 0, Math.Max(0, Rows.Count - ShownRows));
		if (next == FirstShown)
			return false;

		FirstShown = next;
		return true;
	}

	/// <summary>The rectangle of row <paramref name="index"/>, which must be one of those on show.</summary>
	internal static Rectangle RowRect(Vector2 menuAt, int index)
		=> WheelLayout.MenuRow(menuAt, Shape, index - FirstShown);

	/// <summary>Which row the cursor is over, counting the rows scrolled past, or -1.</summary>
	internal static int HitTestRow(Vector2 menuAt, Vector2 cursor)
	{
		int shown = WheelLayout.HitTestMenu(menuAt, Shape, cursor);
		return shown >= 0 ? shown + FirstShown : -1;
	}

	private static bool ShowDigits => PaintWheelConfig.Instance?.Appearance.ShowQuickKeys ?? false;

	private static float LeftPad => RowLeftPad + (ShowDigits ? DigitRoom : 0f);

	/// <summary>
	/// Row width this entry needs: pads, name, count, and the room kept at the right. Plus a pixel,
	/// because the panel and row edges are each rounded and an exact fit can round the wrong way.
	/// </summary>
	private static float RowWidthNeeded(MenuRow row)
		=> LeftPad + WheelDrawing.MeasureText(row.Label, RowTextScale).X + CountWidth(row)
			+ TrailingRoom(row.Preview is not null) + ActionRoom(row) + RowRightPad + 2f;

	/// <summary>Three buttons on a saved palette - edit its colours, rename it, throw it away.</summary>
	private static int ActionCount(MenuRow row) => row.Preset >= 0 ? 3 : 0;

	private static float ActionRoom(MenuRow row) => WheelLayout.MenuActionRoom(ActionCount(row));

	/// <summary>Room left for the name once the count and the trailing space are taken out.</summary>
	private static float NameRoom(Rectangle row, MenuRow entry)
		=> row.Width - LeftPad - RowRightPad - TrailingRoom(entry.Preview is not null)
			- CountWidth(entry) - ActionRoom(entry);

	private static float CountWidth(MenuRow row)
		=> row.Count > 0 ? RowCountGap + WheelDrawing.MeasureText(row.Count.ToString(), RowCountScale).X : 0f;

	/// <summary>
	/// Room kept at the right of a row for the preview dots, or the chevron. Shared by measuring and
	/// drawing so a name can be trimmed to exactly what is left.
	/// </summary>
	private static float TrailingRoom(bool previews)
		=> previews ? RowPreviewDots * (WheelLayout.MenuRowHeight * 0.42f + 3f) + 10f : 22f;

	internal static Vector2 MenuCenter(in WheelLayout.Geometry geometry)
		=> WheelLayout.ClampMenuCenter(geometry.MenuCenter, Shape, Main.screenWidth, Main.screenHeight);

	/// <summary>Which button on the hovered row the cursor is over, or -1 for the row itself.</summary>
	internal static int HitTestRowActions(Vector2 menuAt, Vector2 cursor)
	{
		if (PickerInput.HoveredRow < 0 || PickerInput.HoveredRow >= Rows.Count)
			return -1;

		MenuRow entry = Rows[PickerInput.HoveredRow];
		int count = ActionCount(entry);
		if (count <= 0)
			return -1;

		Rectangle row = RowRect(menuAt, PickerInput.HoveredRow);

		for (int i = 0; i < count; i++) {
			if (WheelLayout.MenuRowAction(row, i, count).Contains((int)cursor.X, (int)cursor.Y))
				return i;
		}

		return -1;
	}

	// ---- Renaming ---------------------------------------------------------------------------

	/// <summary>Starts typing a new name for saved palette <paramref name="preset"/>.</summary>
	internal static bool StartRenaming(int preset, PaintWheelConfig config)
	{
		if (preset < 0 || preset >= config.Presets.Count)
			return false;

		Renaming = preset;
		renameText = config.Presets[preset].Name ?? "";
		return true;
	}

	internal static void StopRenaming()
	{
		Renaming = -1;
		renameText = "";
	}

	/// <summary>Keeps the typed name, unless it is blank, and saves. The palette stays the one in use if it was.</summary>
	internal static void CommitRename(PaintWheelConfig config)
	{
		int preset = Renaming;
		string name = renameText.Trim();
		StopRenaming();

		if (preset < 0 || preset >= config.Presets.Count || name.Length == 0 || name == config.Presets[preset].Name)
			return;

		PaletteEditor.RenamePreset(preset, name, config);
	}

	/// <summary>
	/// Reads the keyboard into the name being typed, the way tModLoader's own text fields do. Run from
	/// the draw, once a frame: WritingText is what stops the same keys moving the player or switching
	/// the hotbar, and the game clears it again every update.
	/// </summary>
	private static void ReadRenameInput()
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null || !PaintPicker.IsActive || PaintPicker.Overlay != PickerOverlay.Palettes) {
			StopRenaming();
			return;
		}

		PlayerInput.WritingText = true;
		Main.instance.HandleIME();

		// Too long is cut to fit rather than refused, so a paste still lands and a name already over the
		// limit can still be shortened.
		string next = Main.GetInputText(renameText);
		if (next.Length > MaxNameLength && next.Length > renameText.Length)
			next = next[..Math.Max(MaxNameLength, renameText.Length)];

		renameText = next;

		if (Main.inputTextEnter) {
			// Chat opens on an Enter it has not seen held. A draw can run before the next update, which
			// would no longer be claiming the keyboard, so the press is marked as already seen.
			Main.chatRelease = false;
			CommitRename(config);
		}
		else if (Main.inputTextEscape) {
			StopRenaming();
			PaintPicker.SwallowEscape();
		}
	}

	// ---- Drawing ----------------------------------------------------------------------------

	internal static void DrawMenu(SpriteBatch spriteBatch, in WheelLayout.Geometry geometry, float opacity)
	{
		if (IsRenaming)
			ReadRenameInput();

		if (Rows.Count == 0)
			return;

		WheelLayout.MenuSettings menu = Shape;
		Vector2 center = MenuCenter(geometry);
		Rectangle bounds = WheelLayout.MenuBounds(center, menu);

		WheelDrawing.DrawPanel(spriteBatch, bounds, opacity);
		DrawMenuTitle(spriteBatch, center, menu, opacity);
		DrawScrollMarks(spriteBatch, bounds, opacity);

		bool digits = ShowDigits;
		int last = FirstShown + ShownRows;

		for (int i = FirstShown; i < last; i++) {
			MenuRow entry = Rows[i];
			Rectangle row = RowRect(center, i);
			bool hovered = i == PickerInput.HoveredRow;
			bool renaming = entry.Preset >= 0 && entry.Preset == Renaming;

			if (hovered || renaming)
				WheelDrawing.DrawRect(spriteBatch, row, PickerRenderer.MenuRowTint * (opacity * (renaming ? 0.6f : 0.9f)));

			// A bar rather than an outline: an outline loses against the filled hover state.
			if (entry.Current)
				WheelDrawing.DrawRect(spriteBatch, new Rectangle(row.X, row.Y + 3, 3, row.Height - 6),
					UIColors.Chosen * opacity);

			if (i < last - 1) {
				WheelDrawing.DrawRect(spriteBatch, new Rectangle(row.X + 4, row.Bottom - 1, row.Width - 8, 1),
					Color.Black * (opacity * 0.25f));
			}

			// The number key that picks this row, dim, where the hotbar puts its own numbers.
			int shown = i - FirstShown;
			if (digits && shown < 10) {
				WheelDrawing.DrawSmallNumber(spriteBatch, ((shown + 1) % 10).ToString(),
					new Vector2(row.X + 5f, row.Center.Y - 6f), new Color(150, 154, 184) * opacity, 0.62f);
			}

			Color text = hovered ? Color.White : entry.Current ? UIColors.Chosen : new Color(214, 216, 234);
			var at = new Vector2(row.X + LeftPad, row.Center.Y);

			if (renaming) {
				DrawRenameField(spriteBatch, row, at, entry, opacity);
			}
			else {
				string count = entry.Count > 0 ? entry.Count.ToString() : null;
				string label = WheelDrawing.Truncate(entry.Label, RowTextScale, NameRoom(row, entry));
				WheelDrawing.DrawTextLeft(spriteBatch, label, at, text, opacity, RowTextScale);

				if (count is not null) {
					float after = at.X + WheelDrawing.MeasureText(label, RowTextScale).X + RowCountGap;
					WheelDrawing.DrawTextLeft(spriteBatch, count, new Vector2(after, at.Y),
						new Color(150, 154, 184), opacity, RowCountScale);
				}
			}

			if (entry.Preview is not null)
				DrawPalettePreview(spriteBatch, entry, row, opacity);
			else if (entry.Kind != RowKind.ScrapeTarget)
				DrawRowChevron(spriteBatch, row, hovered, opacity);

			DrawRowActions(spriteBatch, entry, row, hovered, opacity);
		}

		// The candidates of a word being composed, for Chinese and the other languages typed through an
		// IME; drawn only while there are some.
		if (IsRenaming)
			Main.instance.DrawWindowsIMEPanel(new Vector2(bounds.Center.X, bounds.Bottom + 6f + ImePanelHeight), 0.5f);
	}

	/// <summary>Height of vanilla's IME candidate panel, which it draws above the point it is given.</summary>
	private const float ImePanelHeight = 32f;

	/// <summary>
	/// The name being typed, trimmed from the left so the end - where the typing is - stays in view.
	/// A word still being composed through an IME follows it in yellow, the way chat shows one.
	/// </summary>
	private static void DrawRenameField(SpriteBatch spriteBatch, Rectangle row, Vector2 at, MenuRow entry, float opacity)
	{
		float room = NameRoom(row, entry) + CountWidth(entry);
		string composing = Platform.Get<IImeService>().CompositionString ?? "";
		bool caret = composing.Length == 0 && Main.GameUpdateCount / 30 % 2 == 0;
		string shown = renameText + composing;

		while (shown.Length > composing.Length && WheelDrawing.MeasureText(shown + "|", RowTextScale).X > room)
			shown = shown[1..];

		string typed = shown[..(shown.Length - composing.Length)];
		WheelDrawing.DrawTextLeft(spriteBatch, typed + (caret ? "|" : ""), at, Color.White, opacity, RowTextScale);

		if (composing.Length > 0) {
			var after = new Vector2(at.X + WheelDrawing.MeasureText(typed, RowTextScale).X, at.Y);
			WheelDrawing.DrawTextLeft(spriteBatch, composing, after, new Color(255, 240, 20), opacity, RowTextScale);
		}
	}

	/// <summary>Marks that there are rows scrolled out of view above or below, in the panel's padding.</summary>
	private static void DrawScrollMarks(SpriteBatch spriteBatch, Rectangle bounds, float opacity)
	{
		Color mark = new Color(198, 202, 228) * opacity;

		if (FirstShown > 0)
			WheelDrawing.DrawTriangleVertical(spriteBatch, new Vector2(bounds.Center.X, bounds.Top + 4f), 7f, -1, mark);

		if (FirstShown + ShownRows < Rows.Count)
			WheelDrawing.DrawTriangleVertical(spriteBatch, new Vector2(bounds.Center.X, bounds.Bottom - 4f), 7f, 1, mark);
	}

	/// <summary>
	/// The buttons that belong to a saved palette. They sit on the row itself rather than in a menu of
	/// their own, so acting on a palette does not mean first making it the one you are using. Drawn with
	/// vanilla's own art where it has some - the character list's rename and delete - dimmed at rest and
	/// full on hover, the way vanilla's image buttons are.
	/// </summary>
	private static void DrawRowActions(SpriteBatch spriteBatch, MenuRow entry, Rectangle row,
		bool hoveredRowNow, float opacity)
	{
		int count = ActionCount(entry);
		if (count <= 0)
			return;

		for (int i = 0; i < count; i++) {
			Rectangle box = WheelLayout.MenuRowAction(row, i, count);
			bool lit = hoveredRowNow && PickerInput.HoveredAction == i;
			bool armed = i == ActionDelete && entry.Preset == PickerInput.ArmedDelete;

			if (armed)
				WheelDrawing.DrawRect(spriteBatch, box, PickerRenderer.EmptyMark * (opacity * 0.35f));
			else if (lit)
				WheelDrawing.DrawRect(spriteBatch, box, Color.White * (opacity * 0.16f));

			float strength = lit || armed ? 1f : 0.65f;
			Vector2 middle = box.Center.ToVector2();

			switch (i) {
				case ActionEdit:
					// A brush for "change its colours": there is no vanilla button for that.
					WheelDrawing.DrawItemIcon(spriteBatch, ItemID.Paintbrush, middle, box.Width / 30f,
						Color.White * (opacity * strength));
					break;

				case ActionRename:
					DrawButton(spriteBatch, UITextures.Rename, box,
						Color.White * (opacity * strength),
						() => WheelDrawing.DrawSliders(spriteBatch, middle, box.Width * 0.52f, Color.White * (opacity * strength)));
					break;

				default:
					Color bin = (armed ? PickerRenderer.EmptyMark : Color.White) * (opacity * strength);
					DrawButton(spriteBatch, UITextures.Delete, box, bin,
						() => WheelDrawing.DrawTrash(spriteBatch, middle, box.Width * 0.46f, bin));
					break;
			}
		}
	}

	/// <summary>A button texture fitted to its box, or the hand-drawn fallback if the art is missing.</summary>
	private static void DrawButton(SpriteBatch spriteBatch, Texture2D texture, Rectangle box, Color color, Action fallback)
	{
		if (texture is null) {
			fallback();
			return;
		}

		float scale = box.Width / (float)Math.Max(texture.Width, texture.Height);
		spriteBatch.Draw(texture, box.Center.ToVector2(), null, color, 0f, texture.Size() * 0.5f, scale,
			SpriteEffects.None, 0f);
	}

	private static void DrawMenuTitle(SpriteBatch spriteBatch, Vector2 center,
		in WheelLayout.MenuSettings menu, float opacity)
	{
		if (!menu.Titled)
			return;

		Rectangle band = WheelLayout.MenuTitle(center, menu);
		bool palettes = PaintPicker.DrawOverlay == PickerOverlay.Palettes;

		string title = Language.GetTextValue(palettes
			? "Mods.PaintWheel.UI.PaletteTitle"
			: "Mods.PaintWheel.UI.Scrape.Header");

		// The band doubles as the readout for the buttons, which are too small to label themselves.
		if (palettes) {
			if (IsRenaming)
				title = Language.GetTextValue("Mods.PaintWheel.UI.RenameHint");
			else if (PickerInput.HoveredPlus)
				title = Language.GetTextValue(PaintInventory.OwnsAnyPaint(Main.LocalPlayer)
					? "Mods.PaintWheel.UI.NewPalette"
					: "Mods.PaintWheel.UI.NewEmptyPalette");
			else if (PickerInput.HoveredAction == ActionEdit)
				title = Language.GetTextValue("Mods.PaintWheel.UI.EditPalette");
			else if (PickerInput.HoveredAction == ActionRename)
				title = Language.GetTextValue("Mods.PaintWheel.UI.RenamePalette");
			else if (PickerInput.HoveredAction == ActionDelete)
				title = Language.GetTextValue(PickerInput.ArmedDelete >= 0
					? "Mods.PaintWheel.UI.ConfirmDelete"
					: "Mods.PaintWheel.UI.DeletePalette");
		}

		// Trimmed to the band, less the "+" on the palette list, so a long translation cannot run out of it.
		float room = band.Width - 14f - (palettes ? WheelLayout.MenuActionSize + 4f : 0f);
		title = WheelDrawing.Truncate(title, 0.78f, room);

		WheelDrawing.DrawTextLeft(spriteBatch, title, new Vector2(band.X + 10f, band.Center.Y - 1f),
			Main.OurFavoriteColor, opacity, 0.78f);

		if (palettes) {
			Rectangle add = WheelLayout.MenuTitleAction(center, menu);

			if (PickerInput.HoveredPlus)
				WheelDrawing.DrawRect(spriteBatch, add, Color.White * (opacity * 0.16f));

			Color plus = Color.White * (opacity * (PickerInput.HoveredPlus ? 1f : 0.65f));
			DrawButton(spriteBatch, UITextures.Plus, add, plus, () => WheelDrawing.DrawPlus(spriteBatch, add.Center.ToVector2(), add.Width * 0.5f, plus));
		}

		WheelDrawing.DrawRect(spriteBatch, new Rectangle(band.X + 4, band.Bottom - 1, band.Width - 8, 1),
			Color.Black * (opacity * 0.35f));
	}

	/// <summary>A small arrow on rows that lead somewhere rather than setting something.</summary>
	private static void DrawRowChevron(SpriteBatch spriteBatch, Rectangle row, bool hovered, float opacity)
	{
		// Pointing back, since the one row that has it - back to colours - leaves the list.
		var at = new Vector2(row.Right - 12f, row.Center.Y);

		WheelDrawing.DrawTriangle(spriteBatch, at + Vector2.One, 10f, -1, Color.Black * (opacity * 0.5f));
		WheelDrawing.DrawTriangle(spriteBatch, at, 10f, -1,
			(hovered ? Color.White : new Color(198, 202, 228)) * opacity);
	}

	/// <summary>A few dots of what a palette holds, so rows are told apart by colour, not just name.</summary>
	private static void DrawPalettePreview(SpriteBatch spriteBatch, MenuRow entry, Rectangle row, float opacity)
	{
		const int maxDots = RowPreviewDots;

		Palette palette = entry.Preview;
		int count = Math.Min(maxDots, palette.Paints.Count);
		if (count <= 0)
			return;

		float size = row.Height * 0.42f;
		float step = size + 3f;

		// Stopping where the row's buttons begin, which is the room the width was measured to include.
		float right = row.Right - ActionRoom(entry) - 8f - size * 0.5f;

		for (int i = 0; i < count; i++) {
			// Spread the sample across the palette rather than showing only its first few colours.
			int pick = palette.Paints.Count <= maxDots ? i : i * palette.Paints.Count / maxDots;
			var at = new Vector2(right - (count - 1 - i) * step, row.Center.Y);

			WheelDrawing.DrawDisc(spriteBatch, at, size * 0.5f + 1f, PickerRenderer.RimColor * opacity);
			WheelDrawing.DrawDisc(spriteBatch, at, size * 0.5f - 0.5f, PaintCatalog.AccentColor(palette.Paints[pick]) * opacity);
		}
	}
}
