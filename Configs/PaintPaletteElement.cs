using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PaintWheel.Common;
using PaintWheel.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;

namespace PaintWheel.Configs;

/// <summary>
/// A preset's paint list as one palette block instead of a list of item pickers. The stock UI for a
/// <c>List&lt;ItemDefinition&gt;</c> gives every entry a row, an expand button and a chooser holding
/// every item in the game. Here it is one grid of the loaded paints, in Painter order, click to toggle.
/// </summary>
public class PaintPaletteElement : ConfigElement<List<ItemDefinition>>
{
	/// <summary>Twelve, so each column lines a hue up with its Deep variant and the specials get a row.</summary>
	private const int Columns = 12;

	private const int CellSize = 26;
	private const int CellGap = 5;
	private const int LabelRow = 30;
	private const int Margin = 8;

	private static readonly Color CellRim = new(20, 20, 30);
	private static readonly Color ChosenRim = new(255, 216, 122);

	private readonly List<int> offered = new();
	private int hovered = -1;

	public override void OnBind()
	{
		base.OnBind();

		offered.Clear();
		offered.AddRange(PaintCatalog.Paints);

		int rows = Math.Max(1, (offered.Count + Columns - 1) / Columns);
		Height.Set(LabelRow + rows * (CellSize + CellGap) + Margin, 0f);

		// The label doubles as the readout, so hovering a swatch names it without a tooltip that
		// would cover the grid it belongs to.
		TextDisplayFunction = () => hovered >= 0 && hovered < offered.Count
			? $"{Label}: {Lang.GetItemNameValue(offered[hovered])}"
			: $"{Label} ({Chosen?.Count ?? 0})";

		// The instructions belong on the header, not on the colours. Hovering a swatch is how you read
		// its name off the label, and a paragraph following the cursor around the grid buries it.
		// An empty string is how UIModConfig is told to draw nothing.
		Func<string> instructions = TooltipFunction;
		TooltipFunction = () => hovered >= 0 ? string.Empty : instructions?.Invoke() ?? string.Empty;
	}

	private List<ItemDefinition> Chosen => Value;

	public override void LeftClick(UIMouseEvent evt)
	{
		base.LeftClick(evt);

		int index = CellAt(evt.MousePosition);
		if (index < 0)
			return;

		Toggle(offered[index]);
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	/// <summary>Appends, so click order is ring order. Clicking a chosen paint removes it.</summary>
	private void Toggle(int itemType)
	{
		List<ItemDefinition> list = Value ?? new List<ItemDefinition>();

		int at = IndexOf(list, itemType);
		if (at >= 0)
			list.RemoveAt(at);
		else
			list.Add(new ItemDefinition(itemType));

		// Assigning the same reference back is what flags the config as dirty.
		Value = list;
	}

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		// Resolved before the base draw, which is what reads the label and the tooltip, so both describe
		// the cell under the cursor this frame rather than last frame's.
		CalculatedStyle dimensions = GetDimensions();
		hovered = IsMouseHovering ? CellAt(Main.MouseScreen) : -1;

		base.DrawSelf(spriteBatch);

		if (offered.Count == 0) {
			WheelDrawing.DrawText(spriteBatch, Language.GetTextValue("Mods.PaintWheel.UI.NoPaints"),
				new Vector2(dimensions.X + Margin, dimensions.Y + LabelRow), Color.White, 1f, 0.8f);
			return;
		}

		List<ItemDefinition> chosen = Chosen;

		for (int i = 0; i < offered.Count; i++) {
			int type = offered[i];
			Rectangle cell = CellRect(dimensions, i);
			int order = OrderOf(chosen, type);

			// Opaque, never faded. Fading an unchosen swatch let the blue of the config panel through
			// every colour, which turned two thirds of the palette into mud; a chosen one is called out
			// by its border instead, and the rest are only nudged darker.
			Color accent = PaintCatalog.AccentColor(type);
			WheelDrawing.DrawRect(spriteBatch, cell, order > 0 ? accent : Color.Lerp(accent, CellRim, 0.25f));
			WheelDrawing.DrawRectOutline(spriteBatch, cell, 1, CellRim);

			if (order > 0) {
				WheelDrawing.DrawRectOutline(spriteBatch, cell, 2, ChosenRim);
				DrawOrderNumber(spriteBatch, cell, order);
			}

			if (i == hovered)
				WheelDrawing.DrawRectOutline(spriteBatch, cell, 2, Color.White);
		}
	}

	/// <summary>Place in the ring, cornered so it labels the cell without covering the colour.</summary>
	private static void DrawOrderNumber(SpriteBatch spriteBatch, Rectangle cell, int order)
	{
		const float scale = 0.62f;

		string number = order.ToString();
		Vector2 size = FontAssets.ItemStack.Value.MeasureString(number) * scale;

		Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, number,
			cell.Right - size.X - 2f, cell.Bottom - size.Y - 1f, Color.White, Color.Black,
			Vector2.Zero, scale);
	}

	private static int IndexOf(List<ItemDefinition> list, int itemType)
	{
		for (int i = 0; i < list.Count; i++) {
			ItemDefinition definition = list[i];
			if (definition is not null && !definition.IsUnloaded && definition.Type == itemType)
				return i;
		}

		return -1;
	}

	/// <summary>One-based position in the preset, or 0 when the paint is not in it.</summary>
	private static int OrderOf(List<ItemDefinition> list, int itemType)
		=> list is null ? 0 : IndexOf(list, itemType) + 1;

	private static Rectangle CellRect(CalculatedStyle dimensions, int index)
	{
		int column = index % Columns;
		int row = index / Columns;

		return new Rectangle(
			(int)dimensions.X + Margin + column * (CellSize + CellGap),
			(int)dimensions.Y + LabelRow + row * (CellSize + CellGap),
			CellSize, CellSize);
	}

	private int CellAt(Vector2 point)
	{
		CalculatedStyle dimensions = GetDimensions();

		int x = (int)point.X - ((int)dimensions.X + Margin);
		int y = (int)point.Y - ((int)dimensions.Y + LabelRow);
		if (x < 0 || y < 0)
			return -1;

		int column = x / (CellSize + CellGap);
		int row = y / (CellSize + CellGap);
		if (column >= Columns)
			return -1;

		// Reject the gaps between cells, so a click never lands on a neighbour by a pixel.
		if (x % (CellSize + CellGap) >= CellSize || y % (CellSize + CellGap) >= CellSize)
			return -1;

		int index = row * Columns + column;
		return index >= 0 && index < offered.Count ? index : -1;
	}
}
