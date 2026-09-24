using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace PaintWheel.Common.UI.Picker;

/// <summary>Which view, if any, is standing in for the swatches.</summary>
internal enum PickerOverlay
{
	None,
	Palettes,

	/// <summary>Scrape mode's wheel: what the scraper strips, and the way back to the colours.</summary>
	Scrape,

	/// <summary>Every paint at once, for building a palette.</summary>
	Grid,
}

/// <summary>
/// The picker's lifecycle: opening, closing, committing, and moving between its views (the swatches,
/// the palette list, the scrape wheel and the palette editor). This is the one class the rest of the
/// mod talks to; the others in this folder each own one part of it:
/// <list type="bullet">
/// <item><see cref="PickerContent"/> - what is on offer: palettes, the page on show, the bottom row.</item>
/// <item><see cref="PickerInput"/> - reading the mouse: what is hovered, what a click or release means.</item>
/// <item><see cref="PickerMenu"/> - the palette list: its rows, size, hit testing and drawing.</item>
/// <item><see cref="ScrapeWheel"/> - scrape mode's own wheel: its options and their drawing.</item>
/// <item><see cref="PaletteEditor"/> - building a palette in game, and creating and deleting saved ones.</item>
/// <item><see cref="PickerRenderer"/> - drawing everything else: the swatches, header and bottom row.</item>
/// <item><see cref="WheelLayout"/>, <see cref="WheelMath"/>, <see cref="WheelPaging"/> - where things go, for
/// every layout; pure, with no game state.</item>
/// </list>
/// Small types live beside their users: <see cref="PickerOverlay"/> here, <see cref="Palette"/> in its own
/// file, and <see cref="MenuRow"/> and <see cref="RowKind"/> in PickerMenu.cs.
/// Palettes and pages are separate axes. A palette is a named set of paints, chosen from a list; a page
/// is one ring's worth of the palette you are on, walked with the arrows or the scroll wheel.
/// <para/>
/// All coordinates are UI space: UpdateUI and interface layers both run inside PlayerInput.SetZoom_UI,
/// which has already divided Main.mouseX/Y and screenWidth/Height by Main.UIScale.
/// </summary>
public static class PaintPicker
{
	/// <summary>True while the picker is taking input; it keeps drawing a little longer, while it fades.</summary>
	private static bool active;

	/// <summary>Opened by the keybind rather than right click, which decides what commits and dismisses it.</summary>
	internal static bool ViaKeybind;

	/// <summary>Left up after the button is released, so clicks choose; see PickerOpenMode.Click.</summary>
	internal static bool Sticky;

	internal static int FramesOpen;

	/// <summary>Open animation, 0 to 1: grows while active, shrinks faster while closing.</summary>
	internal static float Anim;

	/// <summary>Where the picker is centred, in UI space: the cursor at opening, nudged on screen.</summary>
	internal static Vector2 Anchor;

	/// <summary>Which view, if any, stands in for the swatches right now.</summary>
	internal static PickerOverlay Overlay;

	/// <summary>
	/// What the drawing reads: tracks <see cref="Overlay"/> while the picker is up, then holds still once
	/// it starts closing. A row that closes the picker usually changes the overlay too, and the fade
	/// takes a few frames, so drawing the live value flashes whatever was switched to.
	/// </summary>
	internal static PickerOverlay DrawOverlay;
	/// <summary>An open asked for and not yet started: whether it came from the keybind, or null for none.</summary>
	private static bool? openRequest;

	/// <summary>
	/// The view the scrape key asked for with the picker up - the scrape wheel, or back to the colours -
	/// shown in the next update, like an open: both go up at the cursor, and the key is read in the
	/// player's update, where the cursor is not yet in UI space.
	/// </summary>
	private static PickerOverlay? viewRequest;

	/// <summary>
	/// Leaving scrape mode from the picker has just put back what was in hand. If that cannot hold the
	/// picker up, the next update closes it - and says why, which is otherwise left unsaid outside the
	/// inventory, where nothing about this changed.
	/// </summary>
	private static bool handSwappedBack;

	/// <summary>A number key pressed while the picker was up: which swatch or row, or -1 for none.</summary>
	private static int quickPick = -1;
	private static int openSuppression;

	/// <summary>True while the picker is accepting input.</summary>
	public static bool IsActive => active;

	/// <summary>
	/// True while the picker has the mouse: while it is up, and after it closes until both buttons are
	/// let go. Vanilla acts on a button that is merely held - a stack pulled onto the cursor a piece at a
	/// time, a shop's buy, the craft button, the hotbar, a swing at the world - so neither the press that
	/// closed the picker nor the right click still held from hold mode may reach any of them.
	/// </summary>
	public static bool HasMouse => active || releasePending;

	/// <summary>Set as the picker closes; cleared once both buttons are up.</summary>
	private static bool releasePending;

	private static void ReleaseWhenLetGo()
	{
		if (releasePending && !Main.mouseLeft && !Main.mouseRight)
			releasePending = false;
	}

	/// <summary>Draws the picker, or nothing while it is fully closed. Drawn after the HUD it covers.</summary>
	public static void Draw(SpriteBatch spriteBatch)
	{
		// Claimed in the draw as well as the update, since vanilla clears it as each frame starts drawing,
		// and the HUD under the picker - which would claim it over itself - cannot see the mouse.
		if (HasMouse && Main.LocalPlayer is Player player)
			player.mouseInterface = true;

		PickerRenderer.Draw(spriteBatch);
		ReleaseWhenLetGo();
	}

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

		openRequest = viaKeybind;
	}

	/// <summary>
	/// What a picker that is not on screen must not still remember: what the cursor was over, and which
	/// list was open. Shared by the end of the close animation and by <see cref="Reset"/> so there is
	/// one list to keep up to date rather than two that drift.
	/// </summary>
	private static void ClearTransient()
	{
		PaletteEditor.StopEditing();
		PickerMenu.StopRenaming();
		PickerMenu.ForgetOpenList();
		PickerInput.ClearHover();
		Overlay = PickerOverlay.None;
		DrawOverlay = PickerOverlay.None;
	}

	public static void Reset()
	{
		active = false;
		Sticky = false;
		Anim = 0f;
		openRequest = null;
		viewRequest = null;
		handSwappedBack = false;
		quickPick = -1;
		releasePending = false;
		openSuppression = 0;
		FramesOpen = 0;
		ViaKeybind = false;
		Anchor = Vector2.Zero;
		PickerInput.ResetButtons();
		ClearTransient();
		PickerContent.Clear();
		PickerMenu.Clear();
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

		if (openRequest is bool keybind) {
			openRequest = null;
			if (!active)
				BeginOpen(keybind, config);
		}

		if (escapeSwallowed && !Main.keyState.IsKeyDown(Keys.Escape) && !Keyboard.GetState().IsKeyDown(Keys.Escape))
			escapeSwallowed = false;

		// Closed, but a button that was down for it still is: the world must not take it as a swing either.
		ReleaseWhenLetGo();
		if (!active && releasePending && Main.LocalPlayer is Player holder)
			holder.mouseInterface = true;

		if (active) {
			PickerMenu.ClaimTextInput();

			bool swappedBack = handSwappedBack;
			handSwappedBack = false;

			if (ShouldCancel(config)) {
				// Said when it is what is in hand - over the inventory, or after "Back to colours" put a
				// block back there - so the picker does not just vanish.
				if (!EnvironmentBlocked() && (!PaintToolSet.InventoryAllows(Main.LocalPlayer) || (swappedBack && HandBlocked(config))))
					Refuse("Mods.PaintWheel.UI.PaintToolNeeded");

				Close(commit: false, config);
			}
			else {
				FramesOpen++;
				Main.LocalPlayer.mouseInterface = true;

				if (viewRequest is PickerOverlay view) {
					viewRequest = null;

					if (view == PickerOverlay.Scrape && PaintSelection.Scrape != ScrapeMode.Off) {
						LeaveEditor();
						PickerMenu.StopRenaming();
						ShowScrapeWheel(Main.MouseScreen, config);
					}
					else if (view == PickerOverlay.None && PaintSelection.Scrape == ScrapeMode.Off) {
						ShowColours(config);
					}
				}

				// Sampled before this tick's clicks, so a closing click leaves the fade showing the list.
				DrawOverlay = Overlay;

				PickerInput.SampleButtons();
				PickerContent.ScraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);
				PickerContent.RefreshStacks();
				PickerInput.UpdateHover(config);
				PickerInput.HandleClicks(config);

				// A number key is a choice of its own, and so is a click that closed the picker: the release
				// or click that would otherwise commit whatever the cursor rests on must not follow either
				// and choose a second thing.
				if (!HandleQuickPick(config) && active) {
					PickerInput.HandleScroll(config);

					if (Sticky) {
						// Dismissing is not choosing - in this mode clicking is what chooses.
						if (PickerInput.ShouldDismiss())
							Close(commit: false, config);
					}
					else if (PickerInput.ShouldCommit(config)) {
						Close(commit: true, config);
					}
				}
			}
		}

		float step = 1f / Math.Max(1, config.Appearance.OpenAnimationTicks);
		Anim = MathHelper.Clamp(Anim + (active ? step : -step * 1.6f), 0f, 1f);

		if (!active && Anim <= 0f)
			ClearTransient();

		if (openSuppression > 0)
			openSuppression--;
	}

	private static void BeginOpen(bool keybind, PaintWheelConfig config)
	{
		// Checked first: opening into a state that cancels on the same tick flashes the picker.
		if (EnvironmentBlocked())
			return;

		PickerContent.ScraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);

		// Scrape mode is sticky, but it cannot survive losing the scraper. Before the hand is looked at,
		// since leaving the mode can change what is in it.
		if (PaintSelection.Scrape != ScrapeMode.Off && !PickerContent.ScraperAvailable)
			PaintSelection.ExitScrape();

		// The one refusal worth explaining: over the inventory, what is in hand decides.
		if (!PaintToolSet.InventoryAllows(Main.LocalPlayer)) {
			Refuse("Mods.PaintWheel.UI.PaintToolNeeded");
			return;
		}

		if (HandBlocked(config))
			return;

		bool scraping = PaintSelection.Scrape != ScrapeMode.Off;

		PickerContent.RebuildPalettes(config);
		PickerContent.RebuildCoatingRow(config);
		PickerContent.ApplyPage(config);

		// Scrape mode and a carried scraper both count, or a player with no paint could never reach the
		// button that enters scrape mode.
		if (!scraping && !HasAnythingToShow) {
			Refuse("Mods.PaintWheel.UI.NothingToShow");
			return;
		}

		// A picker reopened while the last one was still fading out would otherwise inherit its hover
		// and a half-finished edit, and a click on a swatch would then edit a palette instead of painting.
		ClearTransient();

		PickerInput.OpenCursor = Main.MouseScreen;
		PickerInput.CursorMoved = false;
		Overlay = scraping ? PickerOverlay.Scrape : PickerOverlay.None;
		DrawOverlay = Overlay;
		BuildMenuRows();
		Anchor = WheelLayout.ClampAnchor(BuildSettings(config, 1f), PickerInput.OpenCursor, Main.screenWidth, Main.screenHeight);

		// Under the cursor itself rather than the anchor, which is placed to fit the bigger colour view.
		if (scraping)
			ScrapeWheel.Open(PickerInput.OpenCursor, config);

		active = true;
		ViaKeybind = keybind;

		// Click mode is up from the start; hold mode can still stick later if the keybind was tapped.
		Sticky = !keybind && config.OpenWithRightClick && config.OpenMode == PickerOpenMode.Click;
		FramesOpen = 0;

		PickerInput.PrimeButtons();
		PickerInput.HoveredSwatch = -1;
		PickerInput.HoveredCoating = -1;
		PickerInput.HoveredArrow = -1;
		PickerInput.HoveredRow = -1;
		PickerInput.HoveredCenter = false;
		PickerInput.HoveredHeader = false;

		PickerContent.RefreshStacks();
		PickerContent.RememberPage();
		Play(SoundID.MenuOpen, config);
	}

	/// <summary>The tick the last refusal was said, so holding the button does not repeat it every frame.</summary>
	private static uint lastRefusal;

	/// <summary>
	/// Says why the picker did not open. Without this the button just seems dead - the same silence
	/// as a mod that is not working at all.
	/// </summary>
	private static void Refuse(string key)
	{
		if (Main.GameUpdateCount - lastRefusal < 90)
			return;

		lastRefusal = Main.GameUpdateCount;
		PaintSelection.Local?.Announce(Language.GetTextValue(key), PaintWheelPlayer.RefusedTextColor);
	}

	private static void Close(bool commit, PaintWheelConfig config)
	{
		bool selected = false;

		if (commit && PickerInput.CursorMoved) {
			if (Overlay == PickerOverlay.Scrape) {
				// An option is chosen by letting go on it. The middle is not: letting go there cancels, as
				// it does on the colour wheel, so going back to the colours takes a click.
				if (PickerInput.HoveredScrape >= 0)
					selected = ChooseScrapeTarget(PickerInput.HoveredScrape);
			}
			else if (Overlay != PickerOverlay.None) {
				if (PickerInput.HoveredRow >= 0)
					selected = ActivateRow(PickerInput.HoveredRow, config);
			}
			else if (PickerContent.IsScrapeButton(PickerInput.HoveredCoating)) {
				// Entered, but its wheel is not shown: the picker is closing, and a view that appears for
				// the length of the fade and vanishes reads as a glitch. It is there next time.
				selected = PaintSelection.EnterScrape();
			}
			else if (PickerContent.IsNoPaintButton(PickerInput.HoveredCoating)) {
				PaintSelection.ToggleNoPaint();
				selected = true;
			}
			else if (PickerContent.IsPaintBothButton(PickerInput.HoveredCoating)) {
				PaintSelection.TogglePaintBoth();
				selected = true;
			}
			else if (PickerInput.HoveredCoating >= 0 && PickerInput.HoveredCoating < PickerContent.CoatingRow.Count) {
				PaintSelection.SelectCoating(PickerContent.CoatingRow[PickerInput.HoveredCoating]);
				selected = true;
			}
			else if (PickerInput.HoveredSwatch >= 0 && PickerInput.HoveredSwatch < PickerContent.Swatches.Count) {
				PaintSelection.SelectPaint(PickerContent.Swatches[PickerInput.HoveredSwatch]);
				selected = true;
			}
		}

		Play(selected ? SoundID.Grab : SoundID.MenuClose, config);

		CloseSilently();
	}

	/// <summary>Puts the picker away without a sound, for when the caller has already made one.</summary>
	internal static void CloseSilently()
	{
		// A name half typed is dropped: closing is not the Enter that keeps it. Nor may a number key
		// pressed as it closed carry over and pick on the next opening's first frame.
		PickerMenu.StopRenaming();
		quickPick = -1;
		viewRequest = null;
		handSwappedBack = false;
		active = false;
		releasePending = true;

		// A palette made with "+" and left empty was never really made, however the editor was left. The
		// grid itself stays, for the fade.
		if (PaletteEditor.Editing && PaintWheelConfig.Instance is PaintWheelConfig config)
			PaletteEditor.DiscardIfEmptyNew(config);

		Sticky = false;
		Overlay = PickerOverlay.None;
		FramesOpen = 0;
		openSuppression = 2;
	}

	internal static void OpenOverlay(PickerOverlay which, PaintWheelConfig config)
	{
		// Synced on the spot: only a close freezes the picture, opening a list should show it now.
		Overlay = which;
		DrawOverlay = which;
		PickerMenu.ForgetOpenList();
		BuildMenuRows();

		PickerInput.HoveredSwatch = -1;
		PickerInput.HoveredCoating = -1;
		PickerInput.HoveredCenter = false;
		PickerInput.HoveredHeader = false;

		PickerInput.ResetMenuCursor();
		Play(SoundID.MenuOpen, config);
	}

	/// <summary>
	/// Whether the centre and the header have a list worth opening: when there is somewhere to switch
	/// to, and also when there is not, since the list is where a palette gets made. Still false with no
	/// paints at all, so an empty picker stays shut.
	/// </summary>
	internal static bool MenuAvailable => PickerContent.Palettes.Count > 1 || PickerContent.Swatches.Count > 0;

	/// <summary>Rebuilds the rows of whichever list is open, after something they show has changed.</summary>
	internal static void BuildMenuRows() => PickerMenu.Build(Overlay);

	/// <summary>Does what row <paramref name="index"/> of the open list is for. False when there is no such row.</summary>
	internal static bool ActivateRow(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= PickerMenu.Rows.Count)
			return false;

		MenuRow row = PickerMenu.Rows[index];

		switch (row.Kind) {
			case RowKind.Palette:
				SwitchPalette(row.Index, config);
				Overlay = PickerOverlay.None;
				return true;

			default:
				return false;
		}
	}

	/// <summary>Switches to scrape mode and shows its wheel, leaving the picker up.</summary>
	internal static bool EnterScrapeFromRow(PaintWheelConfig config)
	{
		if (!PaintSelection.EnterScrape())
			return false;

		// On the button just clicked, so a flick from there reaches every option.
		ShowScrapeWheel(Main.MouseScreen, config);
		return true;
	}

	/// <summary>Puts the scrape wheel up centred on <paramref name="at"/>, which must be in UI space.</summary>
	private static void ShowScrapeWheel(Vector2 at, PaintWheelConfig config)
	{
		Overlay = PickerOverlay.Scrape;
		DrawOverlay = PickerOverlay.Scrape;
		ScrapeWheel.Open(at, config);

		PickerInput.ResetMenuCursor();
		PickerInput.HoveredCoating = -1;
		PickerInput.HoveredScrape = -1;
	}

	/// <summary>Sets the scraper to what scrape wheel option <paramref name="option"/> strips. False outside scrape mode.</summary>
	internal static bool ChooseScrapeTarget(int option)
	{
		if (option < 0 || option >= ScrapeWheel.Options.Length || PaintSelection.Scrape == ScrapeMode.Off)
			return false;

		PaintSelection.SetScrapeTarget(ScrapeWheel.Options[option]);
		return true;
	}

	/// <summary>
	/// The scrape wheel's middle: leaves scrape mode, which puts the scraper back, and shows the colours
	/// with the picker still up to choose one - in hold mode too, where the button is still down. False
	/// when the hand cannot change yet, mid-swing or with an item on the cursor.
	/// </summary>
	internal static bool BackToColours(PaintWheelConfig config)
	{
		if (!HandSwap.CanSwapNow(Main.LocalPlayer))
			return false;

		PaintSelection.ExitScrape();
		ShowColours(config);
		return true;
	}

	/// <summary>
	/// Shows the colours again, centred where the cursor is - as the scrape wheel was, and as the picker
	/// opens. Left where they were, the scraper's own button could sit under the cursor, and the next
	/// release there would go straight back into scrape mode. Must be called with the cursor in UI space.
	/// </summary>
	private static void ShowColours(PaintWheelConfig config)
	{
		handSwappedBack = true;
		Overlay = PickerOverlay.None;
		DrawOverlay = PickerOverlay.None;
		PickerContent.ScraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);
		Anchor = WheelLayout.ClampAnchor(BuildSettings(config, 1f), Main.MouseScreen, Main.screenWidth, Main.screenHeight);

		PickerInput.HoveredScrape = -1;
		PickerInput.HoveredRow = -1;
		PickerInput.ResetMenuCursor();
	}

	internal static void ChangePage(int direction, PaintWheelConfig config)
	{
		if (!PickerContent.StepPage(direction, config))
			return;

		PickerInput.HoveredSwatch = -1;
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

		// Not under the editor grid, where changing palette would leave it editing one out of sight.
		if (active && Overlay == PickerOverlay.Grid)
			return null;

		if (!active) {
			PickerContent.ScraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);
			PickerContent.RebuildPalettes(config);
		}

		if (PickerContent.Palettes.Count <= 1)
			return null;

		CyclePalette(direction, config);

		return PickerContent.ActivePalette?.Name;
	}

	/// <summary>
	/// Asks for swatch or row <paramref name="index"/> of what is on show - number key N picks the Nth.
	/// Deferred to the next update like <see cref="RequestOpen"/>: the key is read during the player
	/// update, and the picker changes only in its own.
	/// </summary>
	public static void RequestQuickPick(int index)
	{
		if (active)
			quickPick = index;
	}

	/// <summary>A number key does what a click on the Nth swatch, row or scrape option would. True when it picked something.</summary>
	private static bool HandleQuickPick(PaintWheelConfig config)
	{
		int index = quickPick;
		quickPick = -1;

		if (index < 0 || !active)
			return false;

		switch (Overlay) {
			case PickerOverlay.None:
				if (index >= PickerContent.Swatches.Count)
					return false;

				PaintSelection.SelectPaint(PickerContent.Swatches[index]);
				Play(SoundID.Grab, config);
				CloseSilently();
				return true;

			case PickerOverlay.Scrape:
				if (index >= ScrapeWheel.Options.Length)
					return false;

				bool chosen = ChooseScrapeTarget(index);
				Play(chosen ? SoundID.Grab : SoundID.MenuClose, config);

				if (chosen)
					CloseSilently();

				return true;

			case PickerOverlay.Palettes:
				int row = PickerMenu.FirstShown + index;
				if (index >= PickerMenu.ShownRows || row >= PickerMenu.Rows.Count)
					return false;

				Play(ActivateRow(row, config) ? SoundID.Grab : SoundID.MenuClose, config);
				return true;
		}

		return false;
	}

	/// <summary>
	/// Steps to the next or previous colour of the palette in use, for a keybind, whether or not the
	/// picker is up. Colours you have none of are passed over, unless the palette holds nothing else.
	/// </summary>
	/// <returns>The paint now chosen, or 0 when there is nothing to step to.</returns>
	public static int StepPaintExternally(int direction)
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null || (active && Overlay == PickerOverlay.Grid))
			return 0;

		if (!active) {
			PickerContent.ScraperAvailable = PaintScraper.HasAny(Main.LocalPlayer);
			PickerContent.RebuildPalettes(config);
		}

		Palette palette = PickerContent.ActivePalette;
		if (palette is null || palette.Paints.Count == 0)
			return 0;

		List<int> paints = palette.Paints;
		int count = paints.Count;
		int from = paints.IndexOf(PaintSelection.Paint);
		bool anyCarried = paints.Exists(type => PaintInventory.TotalStack(Main.LocalPlayer, type) > 0);

		// From nowhere in the palette, the first step lands on its first colour (or its last, going back).
		int start = from >= 0 ? from : direction > 0 ? -1 : count;

		for (int step = 1; step <= count; step++) {
			int at = ((start + direction * step) % count + count) % count;
			int type = paints[at];

			if (type == PaintSelection.Paint || (anyCarried && PaintInventory.TotalStack(Main.LocalPlayer, type) <= 0))
				continue;

			PaintSelection.SelectPaint(type);
			PickerContent.ShowPaletteIndex(at, config);
			return type;
		}

		return 0;
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
				LeaveEditor();
				PickerMenu.StopRenaming();
				viewRequest = PickerOverlay.None;
			}

			return ScrapeMode.Off;
		}

		if (!PaintSelection.EnterScrape())
			return null;

		if (active)
			viewRequest = PickerOverlay.Scrape;

		return PaintSelection.Scrape;
	}

	internal static void CyclePalette(int direction, PaintWheelConfig config)
	{
		if (PickerContent.Palettes.Count <= 1)
			return;

		SwitchPalette((PickerContent.PaletteIndex + direction + PickerContent.Palettes.Count) % PickerContent.Palettes.Count, config);
		Play(SoundID.MenuTick, config);

		// The open list's rows carry the mark for the current palette, so they go stale otherwise.
		if (Overlay == PickerOverlay.Palettes)
			BuildMenuRows();
	}

	private static void SwitchPalette(int index, PaintWheelConfig config)
	{
		if (PickerContent.SelectPalette(index, config))
			PickerInput.HoveredSwatch = -1;
	}

	internal static WheelLayout.Settings BuildSettings(PaintWheelConfig config, float progress) => new() {
		Style = config.Layout,
		Count = PickerContent.Swatches.Count,
		Paged = PickerContent.PageCount(config) > 1,
		CoatingCount = PickerContent.RowSlots,
		ShowHeader = MenuAvailable || PickerContent.PageCount(config) > 1,
		Radius = config.Appearance.WheelRadius,
		Swatch = config.Appearance.SwatchSize,
		DeadZone = config.Appearance.DeadZoneRadius,
		CellWidth = config.Appearance.BarCellWidth,
		CellHeight = config.Appearance.BarCellHeight,
		LabelWidth = WheelDrawing.MeasureText(HeaderLabel(config), HeaderTextScale).X,
		Progress = progress,
	};

	internal const float HeaderTextScale = 0.85f;

	/// <summary>The palette name, plus the page count when there is more than one page.</summary>
	internal static string HeaderLabel(PaintWheelConfig config)
	{
		Palette palette = PickerContent.ActivePalette;
		if (palette is null)
			return null;

		int pages = PickerContent.PageCount(config);

		// The name is trimmed rather than the page count, which is the part that says there is more.
		string name = WheelDrawing.Truncate(palette.Name, HeaderTextScale, WheelLayout.MenuMaxWidth);
		return pages > 1 ? $"{name}  {PickerContent.PageIndex + 1}/{pages}" : name;
	}

	// ---- Editing a palette ------------------------------------------------------------------

	/// <summary>Opens the palette editor on one saved palette, leaving the picker up to click in.</summary>
	internal static bool StartEditing(int index, PaintWheelConfig config)
	{
		if (!PaletteEditor.Begin(index, config))
			return false;

		// Clicking is what edits, so the picker has to stay up even in hold mode.
		Sticky = true;
		Overlay = PickerOverlay.Grid;
		DrawOverlay = PickerOverlay.Grid;
		PickerInput.ResetMenuCursor();

		return true;
	}

	/// <summary>Starts typing a new name for a saved palette. The picker stays up while you type.</summary>
	internal static bool StartRenaming(int preset, PaintWheelConfig config)
	{
		if (!PickerMenu.StartRenaming(preset, config))
			return false;

		Sticky = true;
		return true;
	}

	/// <summary>
	/// The list's "+": a new palette from what you are carrying - or an empty one when that is nothing -
	/// opened straight in the editor, since choosing its colours is the next thing to do.
	/// </summary>
	internal static bool CreateAndEdit(PaintWheelConfig config)
	{
		int preset = PaletteEditor.CreatePreset(config);
		return preset >= 0 && StartEditing(preset, config);
	}

	/// <summary>Leaves the editor for somewhere other than the list, discarding an empty "+" palette as Done would.</summary>
	private static void LeaveEditor()
	{
		if (PaletteEditor.Editing && PaintWheelConfig.Instance is PaintWheelConfig config)
			PaletteEditor.DiscardIfEmptyNew(config);

		PaletteEditor.StopEditing();
	}

	/// <summary>
	/// Leaves the grid for the list it was opened from, rather than putting the picker away: the
	/// palette you have just built is usually the one you then want to use.
	/// </summary>
	internal static void FinishEditing(PaintWheelConfig config)
	{
		// A palette made with "+" and left empty was never really made.
		PaletteEditor.DiscardIfEmptyNew(config);
		PaletteEditor.StopEditing();
		PickerContent.RebuildPalettes(config);
		PickerContent.ApplyPage(config);

		Overlay = PickerOverlay.Palettes;
		DrawOverlay = PickerOverlay.Palettes;
		BuildMenuRows();
		PickerInput.ResetMenuCursor();
	}

	// ---- Sound ------------------------------------------------------------------------------

	internal static void Play(SoundStyle style, PaintWheelConfig config)
	{
		if (config.PlaySounds)
			SoundEngine.PlaySound(style);
	}

	/// <summary>Played by the keybind actions - eyedropper, quick toggle, scrape and no-paint - so they feel like a commit.</summary>
	public static void PlayCommitSound()
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null || config.PlaySounds)
			SoundEngine.PlaySound(SoundID.Grab);
	}

	// ---- Open guards ------------------------------------------------------------------------

	/// <summary>Set by the Escape that cancelled a rename, until the key is let go.</summary>
	private static bool escapeSwallowed;

	/// <summary>Keeps the Escape that just cancelled a rename from also closing the picker.</summary>
	internal static void SwallowEscape() => escapeSwallowed = true;

	/// <summary>
	/// The scrape wheel and the editor grid carry their own content, so they stay up even when the
	/// swatches behind them have nothing to show.
	/// </summary>
	private static bool ShouldCancel(PaintWheelConfig config)
		=> EnvironmentBlocked() || !PaintToolSet.InventoryAllows(Main.LocalPlayer) || HandBlocked(config)
			|| (Overlay is not (PickerOverlay.Scrape or PickerOverlay.Grid) && !HasAnythingToShow);

	/// <summary>"Only while holding a paint tool", and what is in hand is not one - nor a block the Sprayer would paint.</summary>
	private static bool HandBlocked(PaintWheelConfig config)
		=> config.RequirePaintTool && !PaintToolSet.HoldsPaintingItem(Main.LocalPlayer);

	/// <summary>Swatches, coatings, or a list worth opening. Any of the three is reason to be up.</summary>
	private static bool HasAnythingToShow => PickerContent.Swatches.Count > 0 || PickerContent.RowSlots > 0 || MenuAvailable;

	/// <summary>
	/// Everything about the world and the screen that means the picker must not be up - what is in hand
	/// is checked apart (HandBlocked). The inventory is not one of them: over it the picker answers to a
	/// paint tool in hand (PaintToolSet.InventoryAllows), and the HUD under it is kept out of the
	/// mouse's reach meanwhile (PaintWheelUISystem).
	/// </summary>
	private static bool EnvironmentBlocked()
	{
		// inFancyUI covers the mod config and the other full-screen menus, which draw over the layer the
		// picker lives on: a picker opened under one would be live but invisible.
		if (Main.gameMenu || Main.mapFullscreen || Main.ingameOptionsWindow || Main.inFancyUI)
			return true;

		if (Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput)
			return true;

		// An NPC's head or the housing query on the cursor: its click assigns a house wherever it lands,
		// and that layer draws after the picker, where the mouse cannot be kept from it.
		if (Main.instance.mouseNPCType > -1)
			return true;

		// Escape while typing a palette's name only drops the name; the list stays up, for as long as
		// that same press is held.
		if (Main.keyState.IsKeyDown(Keys.Escape) && !PickerMenu.IsRenaming && !escapeSwallowed)
			return true;

		Player player = Main.LocalPlayer;
		return player is null || !player.active || player.dead;
	}
}
