using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.UI.Picker;
using Terraria;
using Terraria.ModLoader;

namespace PaintWheel.Common.GlobalItems;

/// <summary>
/// Turns right click on a paint tool into "open the picker" instead of a swing - and on a block you
/// are placing, when the choice would reach it (see <see cref="PaintToolSet.CanOpenFor"/>). Vanilla only offers
/// alt-function use when no tile interaction happened and the cursor is not over UI, so this cannot
/// fight with opening a chest, and mouseInterface stops it re-triggering while the picker is open.
/// </summary>
public class PickerRightClickGlobalItem : GlobalItem
{
	public override bool AltFunctionUse(Item item, Player player)
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is not null && config.OpenWithRightClick && PaintToolSet.CanOpenFor(item, player))
			return true;

		return base.AltFunctionUse(item, player);
	}

	public override bool CanUseItem(Item item, Player player)
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;

		if (config is not null && config.OpenWithRightClick && player.altFunctionUse == 2
			&& PaintToolSet.CanOpenFor(item, player)) {

			if (player.whoAmI == Main.myPlayer)
				PaintPicker.RequestOpen(viaKeybind: false);

			return false;   // no swing: the right click belongs to the picker
		}

		return base.CanUseItem(item, player);
	}
}
