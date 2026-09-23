using PaintWheel.Common.Configs;
using Terraria;
using Terraria.ID;

namespace PaintWheel.Common.Painting;

/// <summary>Which held items the wheel considers "a painting tool".</summary>
public static class PaintToolSet
{
	// The four brush and roller ids are the ones vanilla hardcodes. Not configurable because they cannot
	// usefully be: PlaceThing_Paintbrush and PlaceThing_PaintRoller test these exact types, so nothing
	// else can paint through vanilla's code however it is registered.

	/// <summary>Scrapers come from the set vanilla's own scraper code tests, so modded ones just work.</summary>
	public static bool IsPaintTool(Item item)
	{
		if (item is null || item.IsAir)
			return false;

		return IsBrushOrRoller(item.type) || ItemID.Sets.IsPaintScraper[item.type];
	}

	/// <summary>A block or a wall: something auto-paint can actually colour as it is placed.</summary>
	public static bool IsPlaceable(Item item)
		=> item is not null && !item.IsAir && (item.createTile > -1 || item.createWall > 0);

	/// <summary>
	/// Whether right click should open the picker. Paint tools always; placeables when the choice would
	/// reach them (auto-paint on, or the restriction off). Nothing else - claiming the button suppresses
	/// the item's own right click, which would break items that have a real one.
	/// </summary>
	public static bool CanOpenFor(Item item, Player player)
	{
		if (IsPaintTool(item))
			return true;

		if (!IsPlaceable(item))
			return false;

		if (AutoPaints(player))
			return true;

		PaintWheelConfig config = PaintWheelConfig.Instance;
		return config is not null && !config.RequirePaintTool;
	}

	/// <summary>
	/// Whether blocks placed now get painted: a Paint Sprayer's effect (or the config's grant of it) with
	/// its builder toggle on. The toggle turns the effect off without removing the flag, so both count.
	/// </summary>
	public static bool AutoPaints(Player player)
		=> player is not null && player.autoPaint && player.builderAccStatus[BuilderToggleSprayer] == 0;

	/// <summary>
	/// What "Only while holding a paint tool" allows: a paint tool, or a block or wall while placing
	/// paints it (the Paint Sprayer's effect). Anything else in hand is not painting.
	/// </summary>
	public static bool HoldsPaintingItem(Player player)
		=> player is not null && (IsPaintTool(player.HeldItem) || (AutoPaints(player) && IsPlaceable(player.HeldItem)));

	/// <summary>Index of the Paint Sprayer's switch in <see cref="Player.builderAccStatus"/>; 0 means on.</summary>
	private const int BuilderToggleSprayer = 3;

	/// <summary>A Paintbrush or Paint Roller of either kind: the tools that go through vanilla's painting.</summary>
	public static bool IsBrushOrRoller(int type)
		=> type is ItemID.Paintbrush or ItemID.SpectrePaintbrush or ItemID.PaintRoller or ItemID.SpectrePaintRoller;

	public static bool IsRoller(int type) => type is ItemID.PaintRoller or ItemID.SpectrePaintRoller;

	// What to reach for from each tool: the other kind, the same grade first.
	private static readonly int[] FromBrush = { ItemID.PaintRoller, ItemID.SpectrePaintRoller };
	private static readonly int[] FromSpectreBrush = { ItemID.SpectrePaintRoller, ItemID.PaintRoller };
	private static readonly int[] FromRoller = { ItemID.Paintbrush, ItemID.SpectrePaintbrush };
	private static readonly int[] FromSpectreRoller = { ItemID.SpectrePaintbrush, ItemID.Paintbrush };

	/// <summary>
	/// Trades the brush in hand for a roller, or the roller for a brush, keeping to the Spectre kind when
	/// there is one. The two just change places: pressing it again trades them back. Holding anything
	/// else it does nothing - the block or scraper it would send to the bag would not come back, and a
	/// scraper that scrape mode put in the hand would leave that mode with nothing to put back.
	/// </summary>
	/// <returns>The item type now held, or 0 when there is nothing to swap to.</returns>
	public static int SwapBrushAndRoller(Player player)
	{
		int[] wanted = player.HeldItem.type switch {
			ItemID.Paintbrush => FromBrush,
			ItemID.SpectrePaintbrush => FromSpectreBrush,
			ItemID.PaintRoller => FromRoller,
			ItemID.SpectrePaintRoller => FromSpectreRoller,
			_ => null,
		};

		if (wanted is null)
			return 0;

		foreach (int type in wanted) {
			if (HandSwap.Hold(player, item => item.type == type, out _))
				return type;
		}

		return 0;
	}

	/// <summary>Whether the tool paints walls. Only a hint for the coating check, so a wrong guess is free.</summary>
	public static bool PaintsWalls(Item item)
	{
		if (item is null || item.IsAir)
			return false;

		return IsRoller(item.type) || item.createWall > 0;
	}
}
