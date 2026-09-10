using System;
using Terraria;
using Terraria.ID;

namespace PaintWheel.Common;

/// <summary>
/// Finding and holding a scraper, so entering scrape mode does not send the player digging for it.
/// Uses <see cref="ItemID.Sets.IsPaintScraper"/> - the same set vanilla's scraper code tests - so
/// modded scrapers work too.
/// </summary>
public static class PaintScraper
{
	/// <summary>Slots that count as "in hand" - the hotbar.</summary>
	private const int HotbarSlots = 10;

	/// <summary>End of the main inventory, past the hotbar.</summary>
	private const int MainSlots = 50;

	public static bool IsScraper(Item item)
		=> item is not null && !item.IsAir && item.stack > 0 && ItemID.Sets.IsPaintScraper[item.type];

	/// <summary>The hotbar first, so a scraper already to hand is never dragged out of the bag.</summary>
	public static int FindSlot(Player player)
	{
		if (player is null)
			return -1;

		for (int slot = 0; slot < HotbarSlots; slot++) {
			if (IsScraper(player.inventory[slot]))
				return slot;
		}

		for (int slot = HotbarSlots; slot < MainSlots; slot++) {
			if (IsScraper(player.inventory[slot]))
				return slot;
		}

		return -1;
	}

	public static bool HasAny(Player player) => FindSlot(player) >= 0;

	/// <summary>Puts a scraper in the player's hand.</summary>
	/// <param name="swappedSlot">
	/// The bag slot it came out of, or -1 when it was already on the hotbar. Kept so leaving can undo it.
	/// </param>
	public static bool Hold(Player player, out int swappedSlot)
	{
		swappedSlot = -1;

		int slot = FindSlot(player);
		if (slot < 0)
			return false;

		// Already on the hotbar: switching to it touches nothing.
		if (slot < HotbarSlots) {
			player.selectedItem = slot;
			return true;
		}

		int hand = Math.Clamp(player.selectedItem, 0, HotbarSlots - 1);
		(player.inventory[hand], player.inventory[slot]) = (player.inventory[slot], player.inventory[hand]);

		player.selectedItem = hand;
		swappedSlot = slot;
		Sync(player, hand, slot);

		return true;
	}

	/// <summary>Undoes <see cref="Hold"/>, skipping the swap if the inventory has moved on since.</summary>
	public static void Release(Player player, int previousSelection, int swappedSlot)
	{
		if (player is null)
			return;

		if (swappedSlot >= HotbarSlots && swappedSlot < MainSlots) {
			int hand = Math.Clamp(player.selectedItem, 0, HotbarSlots - 1);

			// Only if the scraper is still where we put it; otherwise the player rearranged things.
			if (IsScraper(player.inventory[hand])) {
				(player.inventory[hand], player.inventory[swappedSlot]) = (player.inventory[swappedSlot], player.inventory[hand]);
				Sync(player, hand, swappedSlot);
			}
		}

		if (previousSelection >= 0 && previousSelection < HotbarSlots)
			player.selectedItem = previousSelection;
	}

	/// <summary>
	/// Tells the server which slots changed. Scrape mode holds its swap for as long as it is on, and
	/// the server keeps its own copy of the inventory for drops and quick stacking - so without this it
	/// works from the slots as they were before the swap. Vanilla syncs the same way wherever it moves
	/// items itself; see <c>Player.QuickStackAllChests</c>.
	/// </summary>
	private static void Sync(Player player, params int[] slots)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient || player.whoAmI != Main.myPlayer)
			return;

		foreach (int slot in slots) {
			NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI,
				number2: PlayerItemSlotID.Inventory0 + slot, number3: player.inventory[slot].prefix);
		}
	}
}
