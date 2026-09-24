using System;
using Terraria;
using Terraria.ID;

namespace PaintWheel.Common.Painting;

/// <summary>
/// Putting a particular item in the player's hand, for scrape mode and the brush/roller swap: from the
/// hotbar by switching to it, or out of the bag by trading places with whatever is held.
/// </summary>
public static class HandSwap
{
	/// <summary>Slots that count as "in hand" - the hotbar.</summary>
	private const int HotbarSlots = 10;

	/// <summary>End of the main inventory, past the hotbar.</summary>
	private const int MainSlots = 50;

	/// <summary>
	/// What <see cref="Hold"/> moved, so <see cref="Release"/> can move exactly that back: the slot that
	/// was selected, the hotbar slot the item went into, the bag slot it came out of (-1 when it was
	/// already on the hotbar), and the type of the item that was in hand before - swapped into the bag,
	/// or only no longer selected - so it can be found again if it has moved since.
	/// </summary>
	public readonly record struct Swap(int PreviousSelection, int HandSlot, int BagSlot, int DisplacedType)
	{
		public static readonly Swap None = new(-1, -1, -1, 0);
	}

	/// <summary>
	/// Whether the held item can change right now. The same gate vanilla puts on switching hotbar slots
	/// (not mid-swing, nor in the use delay after one), and not while an item rides the cursor, when the
	/// game treats the cursor as the hand. Swapping mid-swing would let whatever lands in the hand fire
	/// on the swing already under way, without the click or the checks its own use needs.
	/// </summary>
	public static bool CanSwapNow(Player player)
		=> player is not null && Main.mouseItem.IsAir && player.selectedItem != 58
			&& player.itemAnimation <= 0 && player.ItemTimeIsZero && player.reuseDelay == 0;

	/// <summary>The hotbar first, so an item already to hand is never dragged out of the bag.</summary>
	public static int FindSlot(Player player, Func<Item, bool> wanted)
	{
		if (player is null)
			return -1;

		for (int slot = 0; slot < MainSlots; slot++) {
			Item item = player.inventory[slot];
			if (item is not null && !item.IsAir && item.stack > 0 && wanted(item))
				return slot;
		}

		return -1;
	}

	/// <summary>Puts the first item matching <paramref name="wanted"/> in the player's hand, recording what that moved.</summary>
	public static bool Hold(Player player, Func<Item, bool> wanted, out Swap swap)
	{
		swap = Swap.None;

		int slot = FindSlot(player, wanted);
		if (slot < 0)
			return false;

		int previous = player.selectedItem;
		int held = previous >= 0 && previous < HotbarSlots ? player.inventory[previous].type : 0;

		// Already on the hotbar: switching to it touches nothing.
		if (slot < HotbarSlots) {
			player.selectedItem = slot;
			swap = new Swap(previous, slot, -1, held);
			return true;
		}

		int hand = Math.Clamp(player.selectedItem, 0, HotbarSlots - 1);
		int displaced = player.inventory[hand].type;
		(player.inventory[hand], player.inventory[slot]) = (player.inventory[slot], player.inventory[hand]);

		player.selectedItem = hand;
		swap = new Swap(previous, hand, slot, displaced);
		Sync(player, hand, slot);

		return true;
	}

	/// <summary>
	/// Undoes <see cref="Hold"/>. Works from the slots it recorded rather than the one selected now, so
	/// scrolling the hotbar in the meantime cannot make it swap the wrong pair; and it skips the swap
	/// when either item has moved, because then the player has rearranged things themselves.
	/// </summary>
	public static void Release(Player player, Swap swap, Func<Item, bool> stillHeld)
	{
		if (player is null)
			return;

		bool swapped = swap.BagSlot >= HotbarSlots && swap.BagSlot < MainSlots
			&& swap.HandSlot >= 0 && swap.HandSlot < HotbarSlots;

		Item hand = swapped ? player.inventory[swap.HandSlot] : null;

		if (swapped && hand is not null && !hand.IsAir && stillHeld(hand)
			&& player.inventory[swap.BagSlot].type == swap.DisplacedType) {

			(player.inventory[swap.HandSlot], player.inventory[swap.BagSlot])
				= (player.inventory[swap.BagSlot], player.inventory[swap.HandSlot]);
			Sync(player, swap.HandSlot, swap.BagSlot);
		}

		// Back to what was held, unless the player has already picked something else to hold.
		if (swap.PreviousSelection >= 0 && swap.PreviousSelection < HotbarSlots && player.selectedItem == swap.HandSlot)
			player.selectedItem = swap.PreviousSelection;
	}

	/// <summary>
	/// Tells the server which slots changed. A swap can stay in place for as long as a mode is on, and
	/// the server keeps its own copy of the inventory for drops and quick stacking - so without this it
	/// works from the slots as they were before. Vanilla syncs the same way wherever it moves items
	/// itself; see <c>Player.QuickStackAllChests</c>.
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
