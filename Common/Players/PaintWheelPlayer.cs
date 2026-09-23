using System;
using Microsoft.Xna.Framework;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Systems;
using PaintWheel.Common.UI;
using PaintWheel.Common.UI.Picker;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;

namespace PaintWheel.Common.Players;

/// <summary>
/// The selection store: the character's paint choices - the selection, its history, the palette and page
/// it was left on, and scrape mode. Those are saved with the character, so the class name is
/// load-bearing - tModLoader files this player's data under it. It also owns <see cref="Announce"/>, the
/// floating confirmation every choice goes through.
/// <para/>
/// Input lives in <see cref="PaintKeybindPlayer"/> and the painting fallbacks in
/// <see cref="PaintOverridePlayer"/>; both reach the choice through the members here.
/// </summary>
public class PaintWheelPlayer : ModPlayer
{
	/// <summary>Item type of the chosen paint, or 0 for "let vanilla decide".</summary>
	public int SelectedPaint { get; private set; }

	/// <summary>Item type of the chosen coating, or 0 for none.</summary>
	public int SelectedCoating { get; private set; }

	/// <summary>The paint chosen before the current one, for the quick-toggle keybind.</summary>
	public int PreviousPaint { get; private set; }

	/// <summary>The palette last opened on. A key rather than an index, since preset lists get reordered.</summary>
	public string ActivePalette { get; private set; } = "";

	public void SetActivePalette(string key) => ActivePalette = key ?? "";

	/// <summary>Page of <see cref="ActivePalette"/> last shown, so a wide palette reopens where it was left.</summary>
	public int ActivePage { get; private set; }

	public void SetActivePage(int page) => ActivePage = Math.Max(0, page);

	/// <summary>Scrape mode and its target. Sticky: it stays on until left, across sessions.</summary>
	public ScrapeMode Scrape { get; private set; } = ScrapeMode.Off;

	/// <summary>
	/// The brush and the roller both reach blocks and walls: each paints its own half, and the other
	/// half when its own is missing or already done. Off means vanilla's split.
	/// </summary>
	public bool PaintBoth { get; private set; }

	public void TogglePaintBoth()
	{
		PaintBoth = !PaintBoth;

		Announce(Language.GetTextValue(PaintBoth
			? "Mods.PaintWheel.UI.PaintBothOn"
			: "Mods.PaintWheel.UI.PaintBothOff"), PaintBoth ? PaintedTextColor : PaletteTextColor);
	}

	// What taking the scraper in hand moved, so leaving scrape mode can put the hotbar back. Session
	// only: restoring slots from a previous session would be meaningless.
	private HandSwap.Swap scrapeSwap = HandSwap.Swap.None;

	/// <summary>
	/// Takes a scraper in hand and turns the mode on. False when the player has no scraper, or is in the
	/// middle of using an item and so cannot change what they hold.
	/// </summary>
	public bool EnterScrapeMode()
	{
		// Guarded even though only the local UI can reach it: it moves real items.
		if (Player.whoAmI != Main.myPlayer || !HandSwap.CanSwapNow(Player))
			return false;

		if (!PaintScraper.Hold(Player, out HandSwap.Swap swap))
			return false;

		// Only on the way in, so re-picking a target later does not overwrite where to put things back.
		if (Scrape == ScrapeMode.Off) {
			scrapeSwap = swap;
			Scrape = ScrapeMode.BlocksAndWalls;
		}

		return true;
	}

	public void SetScrapeTarget(ScrapeMode mode)
	{
		if (mode == ScrapeMode.Off)
			ExitScrapeMode();
		else if (Scrape != ScrapeMode.Off)
			Scrape = mode;
	}

	public void ExitScrapeMode()
	{
		if (Scrape == ScrapeMode.Off || Player.whoAmI != Main.myPlayer)
			return;

		Scrape = ScrapeMode.Off;
		PaintScraper.Release(Player, scrapeSwap);
		scrapeSwap = HandSwap.Swap.None;
	}

	public void SelectPaint(int itemType) => SelectPaint(itemType, announce: true);

	private void SelectPaint(int itemType, bool announce)
	{
		if (itemType == SelectedPaint)
			return;

		// Bare counts as a state worth coming back to, so it goes in the history like a colour does.
		// Zero does not: it means nothing was ever chosen.
		if (SelectedPaint != 0)
			PreviousPaint = SelectedPaint;

		SelectedPaint = itemType;

		// Here rather than at each call site, so every way of choosing confirms itself the same way.
		if (announce)
			AnnouncePaint(itemType);
	}

	public void SelectCoating(int itemType) => SelectedCoating = itemType;

	/// <summary>
	/// Turns bare placement on, or off again by restoring the colour it interrupted. Without the second
	/// half the only way back is to hunt down the swatch you were already using.
	/// </summary>
	public void ToggleNoPaint()
	{
		bool turningOn = SelectedPaint != PaintSelection.NoPaint;

		// The history can hold bare itself, after toggling from a state with no colour behind it.
		// Restoring that would be a no-op and leave the key looking dead, so it falls back to painting.
		int restored = PreviousPaint == PaintSelection.NoPaint ? 0 : PreviousPaint;

		// Announced as a pair rather than by naming the colour that comes back: this is a switch being
		// thrown, and reading the paint's name gives no clue which way it went.
		SelectPaint(turningOn ? PaintSelection.NoPaint : restored, announce: false);

		Announce(Language.GetTextValue(turningOn
			? "Mods.PaintWheel.UI.NoPaintOn"
			: "Mods.PaintWheel.UI.NoPaintOff"), turningOn ? RefusedTextColor : PaintedTextColor);
	}

	/// <summary>Trades the current paint for the previous one. False when there is nothing to trade with.</summary>
	internal bool SwapWithPrevious()
	{
		// Zero is "never chose anything"; the no-paint sentinel is a real choice, so it swaps like a colour.
		if (PreviousPaint == 0 || PreviousPaint == SelectedPaint)
			return false;

		(SelectedPaint, PreviousPaint) = (PreviousPaint, SelectedPaint);
		return true;
	}

	// ---- Persistence ------------------------------------------------------------------------
	// Content keys ("Terraria/RedPaint"), not raw type ids: ids shift when the mod list changes.

	public override void SaveData(TagCompound tag)
	{
		Store(tag, "paint", SelectedPaint);
		Store(tag, "coating", SelectedCoating);
		Store(tag, "previousPaint", PreviousPaint);

		// No item name to store, so the choice is a flag of its own.
		if (SelectedPaint == PaintSelection.NoPaint)
			tag["noPaint"] = true;

		if (PreviousPaint == PaintSelection.NoPaint)
			tag["previousNoPaint"] = true;

		if (!string.IsNullOrEmpty(ActivePalette))
			tag["palette"] = ActivePalette;

		if (ActivePage > 0)
			tag["page"] = ActivePage;

		if (Scrape != ScrapeMode.Off)
			tag["scrape"] = Scrape.ToString();

		if (PaintBoth)
			tag["paintBoth"] = true;
	}

	public override void LoadData(TagCompound tag)
	{
		SelectedPaint = tag.ContainsKey("noPaint") ? PaintSelection.NoPaint : Restore(tag, "paint");
		SelectedCoating = Restore(tag, "coating");
		PreviousPaint = tag.ContainsKey("previousNoPaint") ? PaintSelection.NoPaint : Restore(tag, "previousPaint");
		ActivePalette = tag.ContainsKey("palette") ? tag.GetString("palette") : "";
		ActivePage = tag.ContainsKey("page") ? Math.Max(0, tag.GetInt("page")) : 0;

		// By name, so reordering the enum cannot silently change what mode a character loads in.
		Scrape = tag.ContainsKey("scrape") && Enum.TryParse(tag.GetString("scrape"), out ScrapeMode mode)
			? mode
			: ScrapeMode.Off;

		PaintBoth = tag.ContainsKey("paintBoth");
	}

	private static void Store(TagCompound tag, string key, int itemType)
	{
		if (itemType <= 0 || itemType >= ItemLoader.ItemCount)
			return;

		string name = ItemID.Search.GetName(itemType);
		if (!string.IsNullOrEmpty(name))
			tag[key] = name;
	}

	private static int Restore(TagCompound tag, string key)
	{
		if (!tag.ContainsKey(key))
			return 0;

		var definition = new ItemDefinition(tag.GetString(key));
		if (definition.IsUnloaded)
			return 0;

		int type = definition.Type;
		return type > 0 && type < ItemLoader.ItemCount ? type : 0;
	}

	internal void AnnouncePaint(int itemType)
	{
		if (itemType == PaintSelection.NoPaint) {
			Announce(Language.GetTextValue("Mods.PaintWheel.UI.NoPaintOn"), RefusedTextColor);
			return;
		}

		if (itemType <= 0)
			return;

		string name = Lang.GetItemNameValue(itemType);

		// Choosing a paint you are out of is allowed - a preset keeps its place - but the brush will do
		// nothing with it, so that is said now rather than discovered at the wall.
		if (PaintInventory.TotalStack(Player, itemType) <= 0)
			Announce($"{name}   {Language.GetTextValue("Mods.PaintWheel.UI.Empty")}", RefusedTextColor);
		else
			Announce(name, PaintCatalog.AccentColor(itemType));
	}

	// The chosen paint and coating, and how much of each was carried at the end of the last tick.
	private int watchedPaint;
	private int watchedPaintStack;
	private int watchedCoating;
	private int watchedCoatingStack;

	/// <summary>
	/// Says so when the chosen paint or coating runs out mid-build. Painting then stops rather than
	/// spend a different colour, and without this the only sign is a tile that did not change.
	/// </summary>
	public override void PostUpdate()
	{
		if (Player.whoAmI != Main.myPlayer)
			return;

		WatchSupply(SelectedPaint, ref watchedPaint, ref watchedPaintStack);
		WatchSupply(SelectedCoating, ref watchedCoating, ref watchedCoatingStack);
	}

	private void WatchSupply(int type, ref int watched, ref int lastStack)
	{
		int stack = type > 0 ? PaintInventory.TotalStack(Player, type) : 0;

		// A stack picked up onto the cursor is being moved, not used up.
		if (type > 0 && Main.mouseItem.type == type)
			stack += Main.mouseItem.stack;

		// Only a drop to nothing of the same item: switching to a paint you have none of is announced
		// by the switch itself.
		if (type > 0 && type == watched && lastStack > 0 && stack == 0)
			Announce(Language.GetTextValue("Mods.PaintWheel.UI.RanOut", Lang.GetItemNameValue(type)), RefusedTextColor);

		watched = type;
		lastStack = stack;
	}

	/// <summary>
	/// Starts the picker afresh for this character: the page it remembers for each palette belongs to
	/// whoever was playing before. Here rather than on world unload, which runs off the main thread.
	/// </summary>
	public override void OnEnterWorld()
	{
		if (Player.whoAmI == Main.myPlayer)
			PaintPicker.Reset();
	}

	// Which combat text slot the last announcement went to, so the next one can take its place.
	private int announceSlot = -1;
	private string announceText;

	/// <summary>
	/// Floats what was chosen above the player, in its own colour - the confirmation once the picker has
	/// closed. Visual only: the caller owns the sound, or the picker's own would double up.
	/// <para/>
	/// The previous line is retired first. These live for over a second, so flipping a toggle twice
	/// leaves both hanging over your head at once and neither reads as the current state.
	/// </summary>
	internal void Announce(string text, Color color)
	{
		if (Player.whoAmI != Main.myPlayer || string.IsNullOrEmpty(text))
			return;

		if (announceSlot >= 0 && announceSlot < Main.combatText.Length) {
			CombatText previous = Main.combatText[announceSlot];

			// Matched on text as well: the slot may have been recycled for someone else's damage number.
			if (previous.active && previous.text == announceText)
				previous.active = false;
		}

		announceSlot = CombatText.NewText(Player.getRect(), Readable(color), text);
		announceText = text;
	}

	/// <summary>
	/// A paint's own colour, lifted toward white until it reads over the world. The text's outline is
	/// its own colour darkened, so Black or Shadow Paint would otherwise float up as a dark smudge.
	/// </summary>
	private static Color Readable(Color color)
	{
		const float Floor = 0.5f;
		float luminance = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255f;

		return luminance >= Floor ? color : Color.Lerp(color, Color.White, (Floor - luminance) / (1f - luminance));
	}

	internal static readonly Color PaletteTextColor = UIColors.Chosen;
	internal static readonly Color RefusedTextColor = new(255, 152, 152);
	internal static readonly Color PaintedTextColor = new(150, 232, 150);
}
