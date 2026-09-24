using System.Collections.Generic;
using PaintWheel.Common.Systems;
using PaintWheel.Common.UI;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PaintWheel.Common.GlobalItems;

/// <summary>
/// Says which key picks the colour, on the paint itself. The marker beside the cursor shows that
/// something can be taken from the slot; only this can say what to press. See <see cref="Painting.PaintHover"/>.
/// </summary>
public class PaintTooltipGlobalItem : GlobalItem
{
	public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
	{
		if (item.paint <= 0 && item.paintCoating <= 0)
			return;

		List<string> keys = KeybindSystem.EyedropperKey?.GetAssignedKeys();
		if (keys is null || keys.Count == 0)
			return;

		string text = Language.GetTextValue(item.paintCoating > 0
			? "Mods.PaintWheel.UI.SlotPickCoating"
			: "Mods.PaintWheel.UI.SlotPickPaint", KeyName(keys[0]));

		tooltips.Add(new TooltipLine(Mod, "PaintWheelPick", text) { OverrideColor = UIColors.Chosen });
	}

	/// <summary>
	/// XNA names punctuation keys "OemOpenBrackets" and the like, which means nothing on a keyboard, and
	/// mouse buttons "Mouse3", which is no better.
	/// </summary>
	private static string KeyName(string key) => key switch {
		"Mouse1" => Language.GetTextValue("Mods.PaintWheel.UI.Keys.MouseLeft"),
		"Mouse2" => Language.GetTextValue("Mods.PaintWheel.UI.Keys.MouseRight"),
		"Mouse3" => Language.GetTextValue("Mods.PaintWheel.UI.Keys.MouseMiddle"),
		"Mouse4" => Language.GetTextValue("Mods.PaintWheel.UI.Keys.Mouse4"),
		"Mouse5" => Language.GetTextValue("Mods.PaintWheel.UI.Keys.Mouse5"),
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
}
