using System;
using Microsoft.Xna.Framework;
using PaintWheel.Configs;
using PaintWheel.UI;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;

namespace PaintWheel.Common;

/// <summary>Per-player selection state, keybinds, and the inventory-swap fallback.</summary>
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

	// Where the scraper came from, so leaving scrape mode can put the hotbar back. Session only:
	// restoring a slot from a previous session would be meaningless.
	private int scrapeReturnSlot = -1;
	private int scrapeSwapSlot = -1;

	/// <summary>Takes a scraper in hand and turns the mode on. False when the player has no scraper.</summary>
	public bool EnterScrapeMode()
	{
		// Guarded even though only the local UI can reach it: it moves real items.
		if (Player.whoAmI != Main.myPlayer)
			return false;

		int previous = Player.selectedItem;

		if (!PaintScraper.Hold(Player, out int swapped))
			return false;

		// Only on the way in, so re-picking a target later does not overwrite where to put things back.
		if (Scrape == ScrapeMode.Off) {
			scrapeReturnSlot = previous;
			scrapeSwapSlot = swapped;
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
		PaintScraper.Release(Player, scrapeReturnSlot, scrapeSwapSlot);

		scrapeReturnSlot = -1;
		scrapeSwapSlot = -1;
	}

	// Slots swapped on the way into ItemCheck, to be swapped back on the way out. -1 = no swap.
	private int swapA = -1;
	private int swapB = -1;

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
			Announce(itemType);
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

	/// <summary>
	/// Hands over the flag a Paint Sprayer sets, so vanilla paints blocks as they are placed. After
	/// equips, which is where vanilla grants it - and it brings the builder toggle along too.
	/// </summary>
	public override void PostUpdateEquips()
	{
		if (Player.whoAmI != Main.myPlayer)
			return;

		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is not null && config.Advanced.AutoPaintPlacedTiles)
			Player.autoPaint = true;
	}

	// ---- Input ------------------------------------------------------------------------------

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		if (Player.whoAmI != Main.myPlayer || TypingSomewhere())
			return;

		if (PaintWheel.OpenWheelKey is { JustPressed: true } && CanOpenFromKeybind())
			PaintWheelState.RequestOpen(viaKeybind: true);

		if (PaintWheel.EyedropperKey is { JustPressed: true })
			TryEyedropper();

		if (PaintWheel.QuickToggleKey is { JustPressed: true })
			TryQuickToggle();

		if (PaintWheel.PreviousPaletteKey is { JustPressed: true })
			TryCyclePalette(-1);

		if (PaintWheel.NextPaletteKey is { JustPressed: true })
			TryCyclePalette(1);

		if (PaintWheel.ToggleScrapeKey is { JustPressed: true })
			TryToggleScrape();

		if (PaintWheel.ToggleNoPaintKey is { JustPressed: true }) {
			// Announced by SelectPaint, in the restored colour when it is turning back on.
			ToggleNoPaint();
			PaintWheelState.PlayCommitSound();
		}
	}

	private static bool TypingSomewhere()
		=> Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput;

	private bool CanOpenFromKeybind()
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null)
			return false;

		return !config.RequirePaintTool || PaintToolSet.IsPaintTool(Player.HeldItem) || Player.autoPaint;
	}

	/// <summary>
	/// Every slot the mouse passes over, whether or not the cursor is carrying something. Returning
	/// false leaves vanilla's own cursor handling - shift to trash, quick move - exactly as it was.
	/// </summary>
	public override bool HoverSlot(Item[] inventory, int context, int slot)
	{
		if (Player.whoAmI == Main.myPlayer)
			PaintHover.Record(inventory[slot]);

		return false;
	}

	/// <summary>Copies the paint off an inventory slot, or off the tile or wall under the cursor.</summary>
	private void TryEyedropper()
	{
		// A slot under the cursor wins: the world behind an open inventory is not what you are pointing at.
		if (TryEyedropperSlot())
			return;

		int x = Terraria.Player.tileTargetX;
		int y = Terraria.Player.tileTargetY;

		if (!WorldGen.InWorld(x, y, 1))
			return;

		Tile tile = Main.tile[x, y];
		byte tilePaint = tile.HasTile ? tile.TileColor : PaintID.None;
		byte wallPaint = tile.WallType != WallID.None ? tile.WallColor : PaintID.None;

		// A roller-style tool is asking about walls; anything else asks about blocks first.
		byte paintId = PaintToolSet.PaintsWalls(Player.HeldItem)
			? (wallPaint != PaintID.None ? wallPaint : tilePaint)
			: (tilePaint != PaintID.None ? tilePaint : wallPaint);

		if (paintId == PaintID.None)
			return;

		int itemType = PaintCatalog.ItemForPaintId(paintId);
		if (itemType <= 0 || itemType == SelectedPaint)
			return;

		SelectPaint(itemType);
		PaintWheelState.PlayCommitSound();
	}

	private bool TryEyedropperSlot()
	{
		int type = PaintHover.Type;
		if (type <= 0)
			return false;

		Item sample = ContentSamples.ItemsByType[type];

		if (sample.paintCoating > 0)
			SelectCoating(type);
		else
			SelectPaint(type);

		// SelectPaint announces itself; a coating has nothing else to show for it.
		if (sample.paintCoating > 0)
			Announce(Lang.GetItemNameValue(type), PaintCatalog.AccentColor(type));

		PaintWheelState.PlayCommitSound();
		return true;
	}

	/// <summary>Steps palette without the picker. The name floats up, since there is nothing to read it off.</summary>
	private void TryCyclePalette(int direction)
	{
		string palette = PaintWheelState.CyclePaletteExternally(direction);

		if (!string.IsNullOrEmpty(palette))
			Announce(palette, PaletteTextColor);
	}

	private static readonly Color PaletteTextColor = new(255, 216, 122);
	private static readonly Color RefusedTextColor = new(255, 152, 152);
	private static readonly Color PaintedTextColor = new(150, 232, 150);

	/// <summary>
	/// Enters or leaves scrape mode from the keybind. Announced either way: the mode swaps the held tool,
	/// so silence would leave the scraper in hand with nothing to say why.
	/// </summary>
	private void TryToggleScrape()
	{
		ScrapeMode? mode = PaintWheelState.ToggleScrapeExternally();

		if (mode is null) {
			Announce(Language.GetTextValue("Mods.PaintWheel.UI.Scrape.NoScraper"), RefusedTextColor);
			return;
		}

		Announce(Language.GetTextValue(mode == ScrapeMode.Off
			? "Mods.PaintWheel.UI.Scrape.Exit"
			: "Mods.PaintWheel.UI.Scrape.Enter"), PaletteTextColor);

		PaintWheelState.PlayCommitSound();
	}

	private void TryQuickToggle()
	{
		// Zero is "never chose anything"; the no-paint sentinel is a real choice, so it swaps like a colour.
		if (PreviousPaint == 0 || PreviousPaint == SelectedPaint)
			return;

		(SelectedPaint, PreviousPaint) = (PreviousPaint, SelectedPaint);

		Announce(SelectedPaint);
		PaintWheelState.PlayCommitSound();
	}

	private void Announce(int itemType)
	{
		if (itemType == PaintSelection.NoPaint)
			Announce(Language.GetTextValue("Mods.PaintWheel.UI.NoPaintOn"), RefusedTextColor);
		else if (itemType > 0)
			Announce(Lang.GetItemNameValue(itemType), PaintCatalog.AccentColor(itemType));
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
	private void Announce(string text, Color color)
	{
		if (Player.whoAmI != Main.myPlayer || string.IsNullOrEmpty(text))
			return;

		if (announceSlot >= 0 && announceSlot < Main.combatText.Length) {
			CombatText previous = Main.combatText[announceSlot];

			// Matched on text as well: the slot may have been recycled for someone else's damage number.
			if (previous.active && previous.text == announceText)
				previous.active = false;
		}

		announceSlot = CombatText.NewText(Player.getRect(), color, text);
		announceText = text;
	}

	// ---- Inventory-swap fallback ------------------------------------------------------------
	// PreItemCheck/PostItemCheck bracket the vanilla method that both picks and consumes paint, so the
	// swap lasts a fraction of one tick, before any drawing or network diffing.

	public override bool PreItemCheck()
	{
		// A leftover swap means PostItemCheck never ran. Undo before re-arming, or the next Pre measures
		// an already-swapped inventory, decides nothing is needed, and the leftover becomes permanent.
		RestoreSwap();

		if (Player.whoAmI != Main.myPlayer)
			return true;

		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null || config.Advanced.OverrideMode != PaintOverrideMode.InventorySwap)
			return true;

		// Paint tools paint; the Paint Sprayer paints through the same code while holding blocks.
		if (!PaintToolSet.IsPaintTool(Player.HeldItem) && !Player.autoPaint)
			return true;

		int desired = PaintOverride.Resolve(Player, out bool deferToVanilla);
		if (deferToVanilla || desired < 0)
			return true;

		int first = PaintInventory.VanillaFirstIndex(Player);
		if (first < 0 || first == desired)
			return true;

		(Player.inventory[first], Player.inventory[desired]) = (Player.inventory[desired], Player.inventory[first]);
		swapA = first;
		swapB = desired;

		return true;
	}

	public override void PostItemCheck() => RestoreSwap();

	/// <summary>
	/// Second net: <c>Player.ItemCheck</c> calls PostItemCheck without a try/finally, so anything
	/// throwing inside it skips the restore and leaves the swap in place.
	/// </summary>
	public override void PostUpdate() => RestoreSwap();

	private void RestoreSwap()
	{
		if (swapA < 0 || swapB < 0) {
			swapA = swapB = -1;
			return;
		}

		// The swapped-in stack may have hit 0 and been turned to air during the tick. Swapping an
		// empty item back is the correct outcome - the paint ran out - so air is tolerated here.
		if (swapA < Player.inventory.Length && swapB < Player.inventory.Length)
			(Player.inventory[swapA], Player.inventory[swapB]) = (Player.inventory[swapB], Player.inventory[swapA]);

		swapA = swapB = -1;
	}
}
