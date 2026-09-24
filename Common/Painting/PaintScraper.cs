using Terraria;
using Terraria.ID;

namespace PaintWheel.Common.Painting;

/// <summary>
/// Finding and holding a scraper, so entering scrape mode does not send the player digging for it, and
/// putting back what it replaced on the way out. Uses <see cref="ItemID.Sets.IsPaintScraper"/> - the
/// same set vanilla's scraper code tests - so modded scrapers work too. The swapping itself is
/// <see cref="HandSwap"/>'s.
/// </summary>
public static class PaintScraper
{
	public static bool IsScraper(Item item)
		=> item is not null && !item.IsAir && item.stack > 0 && ItemID.Sets.IsPaintScraper[item.type];

	/// <summary>The hotbar first, so a scraper already to hand is never dragged out of the bag.</summary>
	public static int FindSlot(Player player) => HandSwap.FindSlot(player, IsScraper);

	public static bool HasAny(Player player) => FindSlot(player) >= 0;

	/// <summary>Puts a scraper in the player's hand, recording what that moved.</summary>
	public static bool Hold(Player player, out HandSwap.Swap swap) => HandSwap.Hold(player, IsScraper, out swap);

	/// <summary>
	/// Undoes <see cref="Hold"/>: the same two slots traded back, if the scraper and what it displaced
	/// are still where it left them. If the scraper is still in hand after that - things have moved
	/// since, sorted or stacked into a chest, or the record was lost - what was held before comes back
	/// from wherever it is now; and when that is not known or not carried any more, a brush or roller
	/// does, since leaving scrape mode is going back to painting.
	/// </summary>
	public static void Release(Player player, HandSwap.Swap swap)
	{
		HandSwap.Release(player, swap, IsScraper);

		if (player is null || !IsScraper(player.HeldItem) || !HandSwap.CanSwapNow(player))
			return;

		if (swap.DisplacedType > 0) {
			// A scraper was in hand already, so there is nothing else to put back.
			if (ItemID.Sets.IsPaintScraper[swap.DisplacedType])
				return;

			if (HandSwap.Hold(player, item => item.type == swap.DisplacedType, out _))
				return;
		}

		HandSwap.Hold(player, item => PaintToolSet.IsBrushOrRoller(item.type), out _);
	}
}
