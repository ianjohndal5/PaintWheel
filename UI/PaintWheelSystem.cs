using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PaintWheel.Common;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace PaintWheel.UI;

public class PaintWheelSystem : ModSystem
{
	/// <summary>
	/// The one point where our entries are known to exist and hold this language's text, which is what
	/// the Filipino override has to overwrite and remember.
	/// </summary>
	public override void OnLocalizationsLoaded() => Translations.Apply();

	public override void UpdateUI(GameTime gameTime)
	{
		if (Main.dedServ)
			return;

		PaintWheelState.Update(gameTime);
	}

	/// <summary>
	/// Scroll is claimed again here: UpdateInput overwrites whatever UpdateUI zeroed, so this hook -
	/// after the refresh, before the player update - is the only place the hotbar can be stopped.
	/// </summary>
	public override void PreUpdatePlayers()
	{
		if (!Main.dedServ && PaintWheelState.IsActive)
			PlayerInput.ScrollWheelDelta = 0;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		// Inserted before mouse text so item tooltips still draw on top of the wheel.
		int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
		if (index == -1)
			return;

		layers.Insert(index, new LegacyGameInterfaceLayer("PaintWheel: Wheel", () => {
			PaintWheelState.Draw(Main.spriteBatch);
			PaintHover.Draw(Main.spriteBatch);
			return true;
		}, InterfaceScaleType.UI));
	}

	public override void Unload() => PaintWheelState.Reset();
}
