using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace PaintWheel.Common.Players;

/// <summary>
/// The per-player half of forcing the choice onto vanilla: the Paint Sprayer grant, and the
/// inventory-swap fallback for when <see cref="PaintOverrideSystem"/>'s detour is switched off.
/// </summary>
public class PaintOverridePlayer : ModPlayer
{
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

	// Slots swapped on the way into ItemCheck, to be swapped back on the way out. -1 = no swap.
	private int swapA = -1;
	private int swapB = -1;

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
		if (!PaintToolSet.HoldsPaintingItem(Player))
			return true;

		int desired = PaintOverrideSystem.Resolve(Player, out bool deferToVanilla);
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
