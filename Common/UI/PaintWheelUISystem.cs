using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using PaintWheel.Common.UI.Picker;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace PaintWheel.Common.UI;

/// <summary>
/// Hooks the picker into the game's UI pass: its update, its scroll claim and its draw layer. The same
/// layer draws the eyedropper's dot beside the cursor (see <see cref="PaintHover"/>).
/// </summary>
public class PaintWheelUISystem : ModSystem
{
	public override void UpdateUI(GameTime gameTime)
	{
		if (Main.dedServ)
			return;

		PaintPicker.Update(gameTime);
	}

	/// <summary>
	/// Scroll is claimed again here: UpdateInput overwrites whatever UpdateUI zeroed, so this hook -
	/// after the refresh, before the player update - is the only place the hotbar can be stopped.
	/// </summary>
	public override void PreUpdatePlayers()
	{
		if (!Main.dedServ && PaintPicker.IsActive)
			PlayerInput.ScrollWheelDelta = 0;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		// Inserted before mouse text so item tooltips still draw on top of the wheel.
		int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
		if (index == -1)
			return;

		layers.Insert(index, new LegacyGameInterfaceLayer("PaintWheel: Wheel", () => {
			PaintPicker.Draw(Main.spriteBatch);
			DrawHoverMarker(Main.spriteBatch);
			DrawCursorReadout(Main.spriteBatch);
			return true;
		}, InterfaceScaleType.UI));
	}

	/// <summary>
	/// A dot of the hovered paint's own colour beside the cursor. Gold once it is the one selected, so
	/// the mark answers "can I take this" and "have I already" with the same glance.
	/// </summary>
	private static void DrawHoverMarker(SpriteBatch spriteBatch)
	{
		int paint = PaintHover.Type;

		// Nothing to promise if the key it describes is not bound to anything.
		if (paint <= 0 || KeybindSystem.EyedropperKey is null || KeybindSystem.EyedropperKey.GetAssignedKeys().Count == 0)
			return;

		bool chosen = paint == PaintSelection.Paint || paint == PaintSelection.Coating;

		// Above and left of the pointer: vanilla's item tooltip opens below and right of it, and would
		// cover the dot there - taking the "already selected" gold with it.
		Vector2 at = Main.MouseScreen + new Vector2(-11f, -9f);

		WheelDrawing.DrawDisc(spriteBatch, at, 8f, Color.Black * 0.55f);
		WheelDrawing.DrawDisc(spriteBatch, at, 7f, chosen ? UIColors.Chosen : Color.White);
		WheelDrawing.DrawDisc(spriteBatch, at, 5f, PaintCatalog.AccentColor(paint));
	}

	/// <summary>
	/// A small mark above and right of the pointer, in the places vanilla shows nothing to go by: what a
	/// block will get from the Sprayer, the paint-both switch on a brush or roller, and which half a
	/// restricted scraper takes. Only while the world is what is under the cursor.
	/// </summary>
	private static void DrawCursorReadout(SpriteBatch spriteBatch)
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		Player player = Main.LocalPlayer;

		if (config is null || !config.Appearance.ShowCursorReadout || Main.gameMenu || player is null || !player.active
			|| player.dead || PaintPicker.IsActive || player.mouseInterface || Main.playerInventory)
			return;

		Item held = player.HeldItem;
		Vector2 at = Main.MouseScreen + new Vector2(20f, -12f);

		if (PaintToolSet.IsBrushOrRoller(held.type)) {
			if (PaintSelection.PaintBoth)
				WheelDrawing.DrawIcon(spriteBatch, UITextures.PaintBoth, at, 16f, Color.White);

			return;
		}

		if (PaintScraper.IsScraper(held)) {
			DrawScrapeMark(spriteBatch, PaintSelection.Scrape, at);
			return;
		}

		if (!PaintToolSet.IsPlaceable(held) || !PaintToolSet.AutoPaints(player))
			return;

		// A placed block is painted once, so it gets one thing: the coating when there is one to spend -
		// a new block never has it yet - and otherwise the paint. PaintOverrideSystem.Resolve decides
		// the same way.
		int paint = PaintSelection.Paint;
		int coating = PaintSelection.Coating;

		if (coating > 0 && PaintInventory.TotalStack(player, coating) > 0) {
			WheelDrawing.DrawDisc(spriteBatch, at, 7f, Color.Black * 0.6f);
			WheelDrawing.DrawDisc(spriteBatch, at, 6f, PaintCatalog.AccentColor(coating));
		}
		else if (paint > 0) {
			// Struck through once it has run out, as its swatch is: the block goes down unpainted. Unless
			// the game is left to pick a paint then - the fallback setting, or the inventory swap, which
			// cannot keep vanilla from its own - when there is nothing certain to show.
			bool stocked = PaintInventory.TotalStack(player, paint) > 0;
			if (!stocked && (config.Advanced.FallBackToAnyPaintWhenEmpty || config.Advanced.OverrideMode != PaintOverrideMode.Detour))
				return;

			WheelDrawing.DrawDisc(spriteBatch, at, 7f, Color.Black * 0.6f);
			WheelDrawing.DrawDisc(spriteBatch, at, 6f, PaintCatalog.AccentColor(paint) * (stocked ? 1f : 0.4f));

			if (!stocked)
				WheelDrawing.DrawSlash(spriteBatch, at, 8f, new Color(236, 132, 132));
		}
		else if (paint == PaintSelection.NoPaint) {
			WheelDrawing.DrawDisc(spriteBatch, at, 7f, Color.Black * 0.6f);
			WheelDrawing.DrawDisc(spriteBatch, at, 6f, new Color(52, 52, 64));
			WheelDrawing.DrawSlash(spriteBatch, at, 8f, new Color(236, 132, 132));
		}
	}

	/// <summary>Which half a restricted scraper takes: a block, a wall panel, or a coating's ring.</summary>
	private static void DrawScrapeMark(SpriteBatch spriteBatch, ScrapeMode mode, Vector2 at)
	{
		var box = new Rectangle((int)at.X - 5, (int)at.Y - 5, 10, 10);
		Color edge = Color.Black * 0.7f;

		switch (mode) {
			case ScrapeMode.BlocksOnly:
				WheelDrawing.DrawRect(spriteBatch, box, new Color(168, 124, 86));
				WheelDrawing.DrawRectOutline(spriteBatch, box, 1, edge);
				break;

			case ScrapeMode.WallsOnly:
				WheelDrawing.DrawRect(spriteBatch, box, new Color(96, 92, 120));
				WheelDrawing.DrawRect(spriteBatch, new Rectangle(box.X, box.Center.Y, box.Width, 1), new Color(66, 62, 88));
				WheelDrawing.DrawRect(spriteBatch, new Rectangle(box.Center.X, box.Y, 1, box.Height), new Color(66, 62, 88));
				WheelDrawing.DrawRectOutline(spriteBatch, box, 1, edge);
				break;

			case ScrapeMode.CoatingsOnly:
				WheelDrawing.DrawDiscOutline(spriteBatch, at, 6f, 2f, edge);
				WheelDrawing.DrawDiscOutline(spriteBatch, at, 5f, 1f, new Color(222, 238, 255));
				break;
		}
	}

	public override void Unload()
	{
		PaintPicker.Reset();
		PaintHover.Reset();
		UITextures.Unload();
	}
}
