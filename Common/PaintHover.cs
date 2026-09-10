using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PaintWheel.UI;
using Terraria;

namespace PaintWheel.Common;

/// <summary>
/// The paint under the cursor in an inventory slot, so the eyedropper keybind can pick a colour out of
/// the inventory as well as off a tile.
/// <para/>
/// Recorded from <c>ModPlayer.HoverSlot</c>, which vanilla calls from <c>ItemSlot.OverrideHover</c> for
/// every slot the mouse passes over, before it handles that slot's clicks - and equally while the
/// cursor is already carrying an item, which is the case worth having.
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

	/// <summary>
	/// A dot of the paint's own colour beside the cursor. Gold once it is the one selected, so the mark
	/// answers "can I take this" and "have I already" with the same glance.
	/// </summary>
	public static void Draw(SpriteBatch spriteBatch)
	{
		int paint = Type;

		// Nothing to promise if the key it describes is not bound to anything.
		if (paint <= 0 || PaintWheel.EyedropperKey is null || PaintWheel.EyedropperKey.GetAssignedKeys().Count == 0)
			return;

		bool chosen = paint == PaintSelection.Paint || paint == PaintSelection.Coating;
		Vector2 at = Main.MouseScreen + new Vector2(19f, 21f);

		WheelDrawing.DrawDisc(spriteBatch, at, 8f, Color.Black * 0.55f);
		WheelDrawing.DrawDisc(spriteBatch, at, 7f, chosen ? new Color(255, 216, 122) : Color.White);
		WheelDrawing.DrawDisc(spriteBatch, at, 5f, PaintCatalog.AccentColor(paint));
	}
}
