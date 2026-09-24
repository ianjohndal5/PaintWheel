using Terraria;
using Terraria.ID;

namespace PaintWheel.Common.Painting;

/// <summary>
/// Finding and holding a scraper, so entering scrape mode does not send the player digging for it.
/// Uses <see cref="ItemID.Sets.IsPaintScraper"/> - the same set vanilla's scraper code tests - so
/// modded scrapers work too. The swapping itself is <see cref="HandSwap"/>'s.
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

	/// <summary>Undoes <see cref="Hold"/>, if the scraper and what it displaced are still where it left them.</summary>
	public static void Release(Player player, HandSwap.Swap swap) => HandSwap.Release(player, swap, IsScraper);
}
