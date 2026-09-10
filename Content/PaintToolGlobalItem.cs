using System.Collections.Generic;
using PaintWheel.Common;
using PaintWheel.Configs;
using PaintWheel.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PaintWheel.Content;

/// <summary>
/// Turns right click on a paint tool into "open the picker" instead of a swing. Vanilla only offers
/// alt-function use when no tile interaction happened and the cursor is not over UI, so this cannot
/// fight with opening a chest, and mouseInterface stops it re-triggering while the picker is open.
/// </summary>
public class PaintToolGlobalItem : GlobalItem
{
	/// <summary>
	/// Says which key picks the colour, on the paint itself. The marker beside the cursor shows that
	/// something can be taken from the slot; only this can say what to press.
	/// </summary>
	public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
	{
		if (item.paint <= 0 && item.paintCoating <= 0)
			return;

		List<string> keys = PaintWheel.EyedropperKey?.GetAssignedKeys();
		if (keys is null || keys.Count == 0)
			return;

		string text = Language.GetTextValue(item.paintCoating > 0
			? "Mods.PaintWheel.UI.SlotPickCoating"
			: "Mods.PaintWheel.UI.SlotPickPaint", KeyName(keys[0]));

		tooltips.Add(new TooltipLine(Mod, "PaintWheelPick", text) { OverrideColor = new Color(255, 216, 122) });
	}

	/// <summary>
	/// XNA names punctuation keys "OemOpenBrackets" and the like, which means nothing on a keyboard.
	/// </summary>
	private static string KeyName(string key) => key switch {
		"OemOpenBrackets" => "[",
		"OemCloseBrackets" => "]",
		"OemComma" => ",",
		"OemPeriod" => ".",
		"OemQuestion" => "/",
		"OemSemicolon" => ";",
		"OemQuotes" => "'",
		"OemMinus" => "-",
		"OemPlus" => "+",
		"OemTilde" => "`",
		"OemPipe" or "OemBackslash" => "\\",
		_ => key,
	};

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
				PaintWheelState.RequestOpen(viaKeybind: false);

			return false;   // no swing: the right click belongs to the picker
		}

		return base.CanUseItem(item, player);
	}
}
