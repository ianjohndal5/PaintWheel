using Terraria;

namespace PaintWheel.Common.Painting;

/// <summary>
/// The paint under the cursor in an inventory slot, so the eyedropper keybind can pick a colour out of
/// the inventory as well as off a tile.
/// <para/>
/// Recorded from <c>ModPlayer.HoverSlot</c>, which vanilla calls from <c>ItemSlot.OverrideHover</c> for
/// every slot the mouse passes over, before it handles that slot's clicks - and equally while the
/// cursor is already carrying an item, which is the case worth having.
/// <para/>
/// The eyedropper's other parts: its key is registered in <see cref="Systems.KeybindSystem"/>; the
/// recording and the pick itself are in <see cref="Players.PaintKeybindPlayer"/>; the dot beside the
/// cursor is drawn by <see cref="UI.PaintWheelUISystem"/>; the "press this key" line on the paint's
/// tooltip is <see cref="GlobalItems.PaintTooltipGlobalItem"/>.
/// </summary>
public static class PaintHover
{
	// Hovering is recorded while the inventory draws, which is after triggers are read, so the record
	// is always a frame behind by the time it is used.
	private const uint Grace = 2;

	private static int type;
	private static uint seen;

	/// <summary>The paint or coating under the cursor, or 0 when there is none.</summary>
	public static int Type => type > 0 && Main.GameUpdateCount - seen <= Grace ? type : 0;

	public static void Record(Item item)
	{
		if (item is null || item.IsAir || (item.paint <= 0 && item.paintCoating <= 0))
			return;

		type = item.type;
		seen = Main.GameUpdateCount;
	}

	public static void Reset()
	{
		type = 0;
		seen = 0;
	}
}
