using System.Collections.Generic;
using PaintWheel.Common.Systems;
using Terraria;

namespace PaintWheel.Common.Painting;

/// <summary>
/// Inventory queries that mirror vanilla's own scan: <c>FindPaintOrCoating</c> walks ammo slots 54-57
/// first, then 0-57. Following the same order means our pick and vanilla's cannot disagree.
/// </summary>
public static class PaintInventory
{
	/// <summary>Number of inventory slots vanilla's paint scan looks at.</summary>
	public const int ScannedSlots = 58;

	private static readonly int[] ScanOrder = BuildScanOrder();

	private static int[] BuildScanOrder()
	{
		var order = new int[ScannedSlots];
		int next = 0;

		for (int i = 54; i < ScannedSlots; i++)
			order[next++] = i;

		for (int i = 0; i < 54; i++)
			order[next++] = i;

		return order;
	}

	/// <summary>Index of the first usable stack of <paramref name="itemType"/>, or -1.</summary>
	public static int FindIndex(Player player, int itemType)
	{
		if (player is null || itemType <= 0)
			return -1;

		foreach (int slot in ScanOrder) {
			Item item = player.inventory[slot];
			if (item is not null && item.stack > 0 && item.type == itemType && item.PaintOrCoating)
				return slot;
		}

		return -1;
	}

	/// <summary>Index of the slot vanilla's own scan would land on, or -1 if the player has no paint.</summary>
	public static int VanillaFirstIndex(Player player)
	{
		if (player is null)
			return -1;

		foreach (int slot in ScanOrder) {
			Item item = player.inventory[slot];
			if (item is not null && item.stack > 0 && item.PaintOrCoating)
				return slot;
		}

		return -1;
	}

	/// <summary>
	/// Summed across every scanned slot: players carry two half stacks, and a swatch reading "almost
	/// gone" with 400 one slot over is a lie.
	/// </summary>
	public static int TotalStack(Player player, int itemType)
	{
		if (player is null || itemType <= 0)
			return 0;

		int total = 0;
		for (int slot = 0; slot < ScannedSlots; slot++) {
			Item item = player.inventory[slot];
			if (item is not null && item.stack > 0 && item.type == itemType)
				total += item.stack;
		}

		return total;
	}

	/// <summary>Distinct paint item types the player is carrying, ordered by paint id.</summary>
	public static void CollectOwnedPaints(Player player, List<int> into)
	{
		into.Clear();
		if (player is null)
			return;

		for (int slot = 0; slot < ScannedSlots; slot++) {
			Item item = player.inventory[slot];
			if (item is null || item.stack <= 0 || item.paint <= 0)
				continue;

			if (!into.Contains(item.type))
				into.Add(item.type);
		}

		into.Sort(PaintCatalog.ByPaintId);
	}

	/// <summary>True when the player is carrying at least one paint.</summary>
	public static bool OwnsAnyPaint(Player player)
	{
		if (player is null)
			return false;

		for (int slot = 0; slot < ScannedSlots; slot++) {
			Item item = player.inventory[slot];
			if (item is not null && item.stack > 0 && item.paint > 0)
				return true;
		}

		return false;
	}

	/// <summary>True when the player is carrying at least one coating item.</summary>
	public static bool OwnsAnyCoating(Player player)
	{
		if (player is null)
			return false;

		for (int slot = 0; slot < ScannedSlots; slot++) {
			Item item = player.inventory[slot];
			if (item is not null && item.stack > 0 && item.paintCoating > 0)
				return true;
		}

		return false;
	}
}
