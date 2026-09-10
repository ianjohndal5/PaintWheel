using System.Collections.Generic;
using PaintWheel.Configs;
using Terraria;
using Terraria.ID;

namespace PaintWheel.Common;

/// <summary>Which held items the wheel considers "a painting tool".</summary>
public static class PaintToolSet
{
	/// <summary>
	/// The four ids vanilla hardcodes. Not configurable because it cannot usefully be:
	/// <c>PlaceThing_Paintbrush</c> and <c>PlaceThing_PaintRoller</c> test these exact types, so nothing
	/// else can paint through vanilla's code however it is registered.
	/// </summary>
	private static readonly HashSet<int> Brushes = new() {
		ItemID.Paintbrush, ItemID.SpectrePaintbrush, ItemID.PaintRoller, ItemID.SpectrePaintRoller,
	};

	private static readonly HashSet<int> Rollers = new() {
		ItemID.PaintRoller, ItemID.SpectrePaintRoller,
	};

	/// <summary>Scrapers come from the set vanilla's own scraper code tests, so modded ones just work.</summary>
	public static bool IsPaintTool(Item item)
	{
		if (item is null || item.IsAir)
			return false;

		return Brushes.Contains(item.type) || ItemID.Sets.IsPaintScraper[item.type];
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

		if (player is not null && player.autoPaint)
			return true;

		PaintWheelConfig config = PaintWheelConfig.Instance;
		return config is not null && !config.RequirePaintTool;
	}

	/// <summary>Whether the tool paints walls. Only a hint for the coating check, so a wrong guess is free.</summary>
	public static bool PaintsWalls(Item item)
	{
		if (item is null || item.IsAir)
			return false;

		return Rollers.Contains(item.type) || item.createWall > 0;
	}
}
