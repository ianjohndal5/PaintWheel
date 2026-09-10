using System;
using PaintWheel.Configs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PaintWheel.Common;

/// <summary>
/// Everything that reaches into vanilla's painting code, in one file, so a port to a new Terraria
/// version has one place to check.
/// <para/>
/// Verified against Terraria.Player in TML_2026_06: <c>Player.TryPainting</c> calls the public
/// <c>FindPaintOrCoating()</c> once and hands that same <c>Item</c> to <c>ApplyPaint</c>, which
/// decrements it. Selection and consumption share one reference and cannot disagree, so returning the
/// item we want from a detour is enough - and it never touches the inventory. The inventory swap in
/// <see cref="PaintWheelPlayer.PreItemCheck"/> is a config-selectable fallback for mod conflicts.
/// </summary>
public static class PaintOverride
{
	private static bool hooked;
	private static bool disabledByError;

	public static void Load()
	{
		if (Main.dedServ)
			return;

		On_Player.FindPaintOrCoating += FindPaintOrCoatingDetour;
		On_Player.PlaceThing_PaintScrapper_TryScrapping += ScrapeDetour;
		hooked = true;
	}

	public static void Unload()
	{
		if (hooked) {
			On_Player.FindPaintOrCoating -= FindPaintOrCoatingDetour;
			On_Player.PlaceThing_PaintScrapper_TryScrapping -= ScrapeDetour;
		}

		hooked = false;
		disabledByError = false;
	}

	private static Item FindPaintOrCoatingDetour(On_Player.orig_FindPaintOrCoating orig, Player self)
	{
		try {
			if (disabledByError || self is null || self.whoAmI != Main.myPlayer)
				return orig(self);

			PaintWheelConfig config = PaintWheelConfig.Instance;
			if (config is null)
				return orig(self);

			// Swapping slots can put a colour in vanilla's path but cannot take every colour out of it,
			// so bare placement is the one choice this detour honours in either mode.
			bool bare = PaintSelection.StateOf(self)?.SelectedPaint == PaintSelection.NoPaint;
			if (!bare && config.Advanced.OverrideMode != PaintOverrideMode.Detour)
				return orig(self);

			int slot = Resolve(self, out bool deferToVanilla);
			if (deferToVanilla)
				return orig(self);

			// Must be an item that really lives in the inventory: vanilla decrements what we return.
			return slot >= 0 ? self.inventory[slot] : null;
		}
		catch (Exception exception) {
			Disable("Paint selection override failed and has been disabled for this session.", exception);
			return orig(self);
		}
	}

	/// <summary>
	/// Restricts the scraper to one half of a tile. Vanilla strips the block if it has paint and only
	/// falls through to the wall otherwise, so neither restriction can be a filter applied afterwards:
	/// the restricted paths mirror <c>PlaceThing_PaintScrapper_TryScrapping</c> with the other branch
	/// removed, keeping its early-out, cursor icon and item time.
	/// </summary>
	private static void ScrapeDetour(On_Player.orig_PlaceThing_PaintScrapper_TryScrapping orig,
		Player self, int x, int y)
	{
		try {
			if (disabledByError || self is null || self.whoAmI != Main.myPlayer) {
				orig(self, x, y);
				return;
			}

			ScrapeMode mode = PaintSelection.StateOf(self)?.Scrape ?? ScrapeMode.Off;
			if (mode is ScrapeMode.Off or ScrapeMode.BlocksAndWalls) {
				orig(self, x, y);
				return;
			}

			ScrapeOneHalf(self, x, y, mode == ScrapeMode.WallsOnly);
		}
		catch (Exception exception) {
			Disable("Scrape restriction failed and has been disabled for this session.", exception);
			orig(self, x, y);
		}
	}

	private static void ScrapeOneHalf(Player player, int x, int y, bool walls)
	{
		if (!WorldGen.InWorld(x, y, 1))
			return;

		Tile tile = Main.tile[x, y];

		bool painted = walls
			? tile.WallType != WallID.None
				&& (tile.WallColor != PaintID.None || tile.IsWallInvisible || tile.IsWallFullbright)
			: tile.HasTile
				&& (tile.TileColor != PaintID.None || tile.IsTileInvisible || tile.IsTileFullbright);

		if (!painted)
			return;

		player.cursorItemIconEnabled = true;

		if (!player.ItemTimeIsZero || player.itemAnimation <= 0 || !player.controlUseItem)
			return;

		if (walls) {
			if (WorldGen.paintWall(x, y, PaintID.None, broadCast: true) || WorldGen.paintCoatWall(x, y, PaintID.None, broadcast: true))
				player.ApplyItemTime(player.inventory[player.selectedItem], player.wallSpeed);
		}
		else if (WorldGen.paintTile(x, y, PaintID.None, broadCast: true) || WorldGen.paintCoatTile(x, y, PaintID.None, broadcast: true)) {
			player.ApplyItemTime(player.inventory[player.selectedItem], player.tileSpeed);
		}
	}

	/// <summary>
	/// Stands the mod down after an unexpected failure, and says so in chat. The log alone is no use to
	/// somebody who just sees their colour stop being applied and has no idea a mod gave up.
	/// </summary>
	private static void Disable(string reason, Exception exception)
	{
		if (disabledByError)
			return;

		disabledByError = true;
		PaintWheel.Instance?.Logger.Error(reason, exception);

		if (!Main.dedServ)
			Main.NewText(Language.GetTextValue("Mods.PaintWheel.UI.Disabled"), 255, 140, 140);
	}

	/// <summary>
	/// Decides which inventory slot painting should draw from.
	/// </summary>
	/// <param name="deferToVanilla">
	/// True when the mod has no opinion and vanilla's own scan should run untouched.
	/// </param>
	/// <returns>An inventory slot index, or -1 for "paint nothing".</returns>
	public static int Resolve(Player player, out bool deferToVanilla)
	{
		deferToVanilla = false;

		PaintWheelPlayer state = PaintSelection.StateOf(player);
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (state is null || config is null) {
			deferToVanilla = true;
			return -1;
		}

		int paintType = state.SelectedPaint;
		int coatingType = state.SelectedCoating;
		bool bare = paintType == PaintSelection.NoPaint;

		if (paintType == 0 && coatingType <= 0) {
			deferToVanilla = true;
			return -1;
		}

		int paintSlot = PaintInventory.FindIndex(player, paintType);
		int coatingSlot = PaintInventory.FindIndex(player, coatingType);

		if (paintSlot < 0 && coatingSlot < 0) {
			// Ran out. Quietly spending a different colour is the bug this mod exists to avoid - and
			// asking for bare blocks is a decision, not running out, so it never falls back.
			deferToVanilla = !bare && config.Advanced.FallBackToAnyPaintWhenEmpty;
			return -1;
		}

		if (paintSlot < 0)
			return coatingSlot;

		if (coatingSlot < 0)
			return paintSlot;

		return PreferCoating(player, player.inventory[coatingSlot], config) ? coatingSlot : paintSlot;
	}

	/// <summary>
	/// Vanilla applies one item per action, so with both picked prefer whichever the target is missing:
	/// holding the mouse on a tile then lands the coating and the colour rather than stalling.
	/// </summary>
	private static bool PreferCoating(Player player, Item coating, PaintWheelConfig config)
	{
		if (!config.Advanced.SmartCoating)
			return true;

		int x = Terraria.Player.tileTargetX;
		int y = Terraria.Player.tileTargetY;
		if (!WorldGen.InWorld(x, y, 1))
			return true;

		Tile tile = Main.tile[x, y];
		bool wall = PaintToolSet.PaintsWalls(player.HeldItem);

		bool alreadyCoated = coating.paintCoating switch {
			PaintCatalog.IlluminantCoating => wall ? tile.IsWallFullbright : tile.IsTileFullbright,
			PaintCatalog.EchoCoating => wall ? tile.IsWallInvisible : tile.IsTileInvisible,
			_ => true,
		};

		return !alreadyCoated;
	}
}
