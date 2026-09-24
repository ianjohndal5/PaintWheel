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
	/// <summary>
	/// The inventory's housing page, the one place right click works on the world itself: on a banner
	/// it sends that NPC out of its house, and the banners do not mark the mouse as over the UI. So right
	/// click is left to them there; the keybind still opens the picker.
	/// </summary>
	private static bool HousingPageOpen => Main.playerInventory && Main.EquipPage == 1;

	private static bool OpensPicker(Item item, Player player)
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		return config is not null && config.OpenWithRightClick && !HousingPageOpen && PaintToolSet.CanOpenFor(item, player);
	}

	public override bool AltFunctionUse(Item item, Player player)
		=> OpensPicker(item, player) || base.AltFunctionUse(item, player);

	public override bool CanUseItem(Item item, Player player)
	{
		if (player.altFunctionUse == 2 && OpensPicker(item, player)) {
			if (player.whoAmI == Main.myPlayer)
				PaintPicker.RequestOpen(viaKeybind: false);

			return false;   // no swing: the right click belongs to the picker
		}

		return base.CanUseItem(item, player);
	}
}
