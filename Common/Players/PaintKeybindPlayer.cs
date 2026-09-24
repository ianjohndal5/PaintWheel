using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Systems;
using PaintWheel.Common.UI.Picker;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PaintWheel.Common.Players;

/// <summary>
/// What the keybinds registered in <see cref="KeybindSystem"/> do, and the eyedropper they drive,
/// including recording the hovered inventory paint it reads (see <see cref="PaintHover"/>). Every
/// action here changes the choice held by <see cref="PaintWheelPlayer"/>, and confirms it the same way
/// the picker would.
/// </summary>
public class PaintKeybindPlayer : ModPlayer
{
	private PaintWheelPlayer Selection => Player.GetModPlayer<PaintWheelPlayer>();

	/// <summary>
	/// Number keys that picked something in the open picker and are still held. Vanilla switches the
	/// hotbar for as long as the key is down, so it is kept from seeing the key until it is let go -
	/// otherwise the picker would close and the hand would change a moment later.
	/// </summary>
	private readonly bool[] swallowedHotbar = new bool[10];

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		// Typing in chat, a sign or a chest name is already filtered out: vanilla only reads triggers
		// when none of those is going on (the three TriggersSet.CopyInto calls in Player). The
		// full-screen menus, like this mod's own config, and the full-screen map are not - and a key
		// pressed over either would act unseen.
		if (Player.whoAmI != Main.myPlayer || Main.inFancyUI || Main.mapFullscreen)
			return;

		TakeNumberKeys(triggersSet);

		// "Only while holding a paint tool" covers the keys too, as that setting says. Most of all the
		// scrape key: it swaps a scraper into the hand, which pressed by accident holding a sword would
		// put the sword in the bag.
		bool toolInHand = CanOpenFromKeybind();

		// Over the inventory the key is let through whatever is in hand, so the picker can say what it
		// needs there rather than doing nothing at all.
		if (KeybindSystem.OpenWheelKey is { JustPressed: true } && (toolInHand || Main.playerInventory))
			PaintPicker.RequestOpen(viaKeybind: true);

		if (KeybindSystem.EyedropperKey is { JustPressed: true })
			TryEyedropper(toolInHand);

		if (KeybindSystem.QuickToggleKey is { JustPressed: true } && toolInHand)
			TryQuickToggle();

		if (KeybindSystem.PreviousPaletteKey is { JustPressed: true } && toolInHand)
			TryCyclePalette(-1);

		if (KeybindSystem.NextPaletteKey is { JustPressed: true } && toolInHand)
			TryCyclePalette(1);

		if (KeybindSystem.ToggleScrapeKey is { JustPressed: true })
			TryToggleScrape(toolInHand);

		if (KeybindSystem.ToggleNoPaintKey is { JustPressed: true } && toolInHand) {
			// ToggleNoPaint announces which way the switch went.
			Selection.ToggleNoPaint();
			PaintPicker.PlayCommitSound();
		}

		if (KeybindSystem.PreviousPaintKey is { JustPressed: true } && toolInHand)
			TryStepPaint(-1);

		if (KeybindSystem.NextPaintKey is { JustPressed: true } && toolInHand)
			TryStepPaint(1);

		if (KeybindSystem.SwapToolKey is { JustPressed: true } && toolInHand)
			TrySwapTool();

		if (KeybindSystem.PaintBothKey is { JustPressed: true } && toolInHand) {
			// TogglePaintBoth announces which way the switch went.
			Selection.TogglePaintBoth();
			PaintPicker.PlayCommitSound();
		}
	}

	/// <summary>
	/// While the picker is up, the hotbar's number keys pick its Nth swatch or row instead - using the
	/// player's own bindings for them, so it works wherever they have put the hotbar.
	/// <para/>
	/// One pick per press: a swallowed key reads as just pressed again every tick when it is bound to a
	/// mouse button, since clearing it leaves nothing held in the state the next tick compares against.
	/// </summary>
	private void TakeNumberKeys(TriggersSet triggersSet)
	{
		for (int i = 0; i < swallowedHotbar.Length; i++) {
			string trigger = "Hotbar" + (i + 1);

			if (PaintPicker.IsActive && !swallowedHotbar[i]
				&& PlayerInput.Triggers.JustPressed.KeyStatus.TryGetValue(trigger, out bool pressed) && pressed) {
				PaintPicker.RequestQuickPick(i);
				swallowedHotbar[i] = true;
			}

			if (!swallowedHotbar[i])
				continue;

			if (triggersSet.KeyStatus.TryGetValue(trigger, out bool held) && held)
				triggersSet.KeyStatus[trigger] = false;
			else
				swallowedHotbar[i] = false;
		}
	}

	/// <summary>Steps to the next or previous colour without the picker. SelectPaint announces it.</summary>
	private void TryStepPaint(int direction)
	{
		if (PaintPicker.StepPaintExternally(direction) > 0)
			PaintPicker.PlayCommitSound();
	}

	/// <summary>
	/// Trades the brush for the roller or back, at a moment the hand can change, and names what is now
	/// held - or says there is nothing to swap to.
	/// </summary>
	private void TrySwapTool()
	{
		// Only a brush or roller has a partner to trade with; holding a block or the scraper, the key
		// is not for what is in hand, and "no roller" would be the wrong thing to say.
		if (!PaintToolSet.IsBrushOrRoller(Player.HeldItem.type) || !HandSwap.CanSwapNow(Player))
			return;

		int held = PaintToolSet.SwapBrushAndRoller(Player);

		if (held > 0) {
			Selection.Announce(Lang.GetItemNameValue(held), PaintWheelPlayer.PaletteTextColor);
			PaintPicker.PlayCommitSound();
			return;
		}

		int missing = PaintToolSet.IsRoller(Player.HeldItem.type) ? ItemID.Paintbrush : ItemID.PaintRoller;
		Selection.Announce(Language.GetTextValue("Mods.PaintWheel.UI.NoToolToSwap", Lang.GetItemNameValue(missing)),
			PaintWheelPlayer.RefusedTextColor);
	}

	private bool CanOpenFromKeybind()
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null)
			return false;

		return !config.RequirePaintTool || PaintToolSet.HoldsPaintingItem(Player);
	}

	/// <summary>
	/// Every slot the mouse passes over, whether or not the cursor is carrying something. Returning
	/// false leaves vanilla's own cursor handling - shift to trash, quick move - exactly as it was.
	/// </summary>
	public override bool HoverSlot(Item[] inventory, int context, int slot)
	{
		if (Player.whoAmI == Main.myPlayer)
			PaintHover.Record(inventory[slot]);

		return false;
	}

	/// <summary>
	/// Copies the paint off an inventory slot, or off the tile or wall under the cursor. A slot always
	/// answers, whatever is held - picking a colour out of the inventory is not painting yet.
	/// </summary>
	private void TryEyedropper(bool toolInHand)
	{
		// A slot under the cursor wins: the world behind an open inventory is not what you are pointing at.
		if (TryEyedropperSlot())
			return;

		// Nor is the world behind any other UI, the picker included.
		if (!toolInHand || Player.mouseInterface)
			return;

		int x = Terraria.Player.tileTargetX;
		int y = Terraria.Player.tileTargetY;

		if (!WorldGen.InWorld(x, y, 1))
			return;

		Tile tile = Main.tile[x, y];
		byte tilePaint = tile.HasTile ? tile.TileColor : PaintID.None;
		byte wallPaint = tile.WallType != WallID.None ? tile.WallColor : PaintID.None;

		// A roller-style tool is asking about walls; anything else asks about blocks first.
		byte paintId = PaintToolSet.PaintsWalls(Player.HeldItem)
			? (wallPaint != PaintID.None ? wallPaint : tilePaint)
			: (tilePaint != PaintID.None ? tilePaint : wallPaint);

		if (paintId == PaintID.None)
			return;

		int itemType = PaintCatalog.ItemForPaintId(paintId);
		if (itemType <= 0 || itemType == Selection.SelectedPaint)
			return;

		Selection.SelectPaint(itemType);
		PaintPicker.PlayCommitSound();
	}

	private bool TryEyedropperSlot()
	{
		int type = PaintHover.Type;
		if (type <= 0)
			return false;

		Item sample = ContentSamples.ItemsByType[type];

		if (sample.paintCoating > 0)
			Selection.SelectCoating(type);
		else
			Selection.SelectPaint(type);

		// SelectPaint announces itself; a coating has nothing else to show for it. Picked out of a chest,
		// it may be one you are not carrying, which is worth saying before the brush finds out.
		if (sample.paintCoating > 0) {
			string name = Lang.GetItemNameValue(type);

			if (PaintInventory.TotalStack(Player, type) <= 0)
				Selection.Announce($"{name}   {Language.GetTextValue("Mods.PaintWheel.UI.Empty")}", PaintWheelPlayer.RefusedTextColor);
			else
				Selection.Announce(name, PaintCatalog.AccentColor(type));
		}

		PaintPicker.PlayCommitSound();
		return true;
	}

	/// <summary>Steps palette without the picker. The name floats up, since there is nothing to read it off.</summary>
	private void TryCyclePalette(int direction)
	{
		string palette = PaintPicker.CyclePaletteExternally(direction);

		if (!string.IsNullOrEmpty(palette))
			Selection.Announce(palette, PaintWheelPlayer.PaletteTextColor);
	}

	/// <summary>
	/// Enters or leaves scrape mode from the keybind. Announced either way: the mode swaps the held tool,
	/// so silence would leave the scraper in hand with nothing to say why.
	/// <para/>
	/// Either way it waits for a moment the hand can change - not mid-swing, and not while an item rides
	/// the cursor - since both entering and leaving swap what is held. Entering also wants a paint tool
	/// in hand; leaving does not, since it puts things back.
	/// </summary>
	private void TryToggleScrape(bool toolInHand)
	{
		bool entering = Selection.Scrape == ScrapeMode.Off;
		if ((entering && !toolInHand) || !HandSwap.CanSwapNow(Player))
			return;

		ScrapeMode? mode = PaintPicker.ToggleScrapeExternally();

		if (mode is null) {
			Selection.Announce(Language.GetTextValue("Mods.PaintWheel.UI.Scrape.NoScraper"), PaintWheelPlayer.RefusedTextColor);
			return;
		}

		Selection.Announce(Language.GetTextValue(mode == ScrapeMode.Off
			? "Mods.PaintWheel.UI.Scrape.Exit"
			: "Mods.PaintWheel.UI.Scrape.Enter"), PaintWheelPlayer.PaletteTextColor);

		PaintPicker.PlayCommitSound();
	}

	private void TryQuickToggle()
	{
		if (!Selection.SwapWithPrevious())
			return;

		Selection.AnnouncePaint(Selection.SelectedPaint);
		PaintPicker.PlayCommitSound();
	}
}
