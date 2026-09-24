using System;
using System.Reflection;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PaintWheel.Common.Systems;

/// <summary>
/// The detours into vanilla's painting and scraping code. Together with <see cref="PaintOverridePlayer"/>
/// (the ItemCheck swap and the Paint Sprayer grant) this is everything a port to a new Terraria version
/// has to check; PaintInventory and PaintToolSet mirror vanilla's scan order and tool ids.
/// <para/>
/// Verified against Terraria.Player in TML_2026_07: <c>Player.TryPainting</c> calls the public
/// <c>FindPaintOrCoating()</c> once and hands that same <c>Item</c> to <c>ApplyPaint</c>, which
/// decrements it. Selection and consumption share one reference and cannot disagree, so returning the
/// item we want from a detour is enough - and it never touches the inventory. The inventory swap in
/// <see cref="PaintOverridePlayer.PreItemCheck"/> is a config-selectable fallback for mod conflicts.
/// </summary>
public class PaintOverrideSystem : ModSystem
{
	private static bool disabledByError;

	/// <summary>
	/// The tile and half vanilla's TryPainting is working on right now, so smart coating checks the
	/// half actually being painted - in paint-both mode the brush reaches walls too, and a Sprayer paints
	/// whatever was just placed. Null outside it.
	/// </summary>
	private static (int X, int Y, bool Wall)? painting;

	/// <summary>Vanilla's private TryPainting, called the way its own brush and roller code call it.</summary>
	private static Action<Player, int, int, bool, bool> tryPainting;

	public override void Load()
	{
		if (Main.dedServ)
			return;

		// Nothing to undo in Unload: tModLoader removes every On_ hook a mod added before it unloads it.
		On_Player.FindPaintOrCoating += FindPaintOrCoatingDetour;
		On_Player.PlaceThing_PaintScrapper_TryScrapping += ScrapeDetour;
		On_Player.TryPainting += TryPaintingDetour;
		On_Player.PlaceThing_Paintbrush += BrushDetour;
		On_Player.PlaceThing_PaintRoller += RollerDetour;

		// Missing only if a Terraria update renamed it; paint-both then stands down rather than failing.
		tryPainting = typeof(Player).GetMethod("TryPainting", BindingFlags.Instance | BindingFlags.NonPublic)
			?.CreateDelegate<Action<Player, int, int, bool, bool>>();

		if (tryPainting is null)
			Mod.Logger.Warn("Player.TryPainting was not found; painting blocks and walls with one tool is off.");
	}

	public override void Unload()
	{
		disabledByError = false;
		tryPainting = null;
		painting = null;
	}

	private static void TryPaintingDetour(On_Player.orig_TryPainting orig, Player self, int x, int y,
		bool paintingAWall, bool applyItemAnimation)
	{
		var outer = painting;
		painting = (x, y, paintingAWall);

		try {
			orig(self, x, y, paintingAWall, applyItemAnimation);
		}
		finally {
			painting = outer;
		}
	}

	private static void BrushDetour(On_Player.orig_PlaceThing_Paintbrush orig, Player self)
	{
		if (!PaintsBoth(self)) {
			orig(self);
			return;
		}

		try {
			PaintEitherHalf(self, wallFirst: false);
		}
		catch (Exception exception) {
			Disable("Painting blocks and walls with one tool failed and has been disabled for this session.", exception);
		}
	}

	private static void RollerDetour(On_Player.orig_PlaceThing_PaintRoller orig, Player self)
	{
		if (!PaintsBoth(self)) {
			orig(self);
			return;
		}

		try {
			PaintEitherHalf(self, wallFirst: true);
		}
		catch (Exception exception) {
			Disable("Painting blocks and walls with one tool failed and has been disabled for this session.", exception);
		}
	}

	private static bool PaintsBoth(Player self)
		=> !disabledByError && tryPainting is not null && self is not null && self.whoAmI == Main.myPlayer
			&& PaintSelection.StateOf(self)?.PaintBoth == true;

	/// <summary>
	/// Paint-both: vanilla's PlaceThing_Paintbrush and PlaceThing_PaintRoller with the half left open.
	/// The tool's own half goes first - blocks for the brush, walls for the roller - and when that half
	/// is missing, or already has what would be applied (vanilla then applies nothing and leaves the use
	/// time at zero), the other half is tried in the same use. Holding the mouse on one tile therefore
	/// paints the block and then the wall behind it.
	/// </summary>
	private static void PaintEitherHalf(Player self, bool wallFirst)
	{
		Item held = self.inventory[self.selectedItem];

		// Each detour answers only for its own tool, exactly as the vanilla methods do.
		if (!PaintToolSet.IsBrushOrRoller(held.type) || PaintToolSet.IsRoller(held.type) != wallFirst)
			return;

		// Vanilla's reach test, unchanged.
		int x = Player.tileTargetX;
		int y = Player.tileTargetY;
		float reach = Player.tileRangeX + held.tileBoost + self.blockRange;
		float reachY = Player.tileRangeY + held.tileBoost + self.blockRange;

		if (!(self.position.X / 16f - reach <= x) || !((self.position.X + self.width) / 16f + reach - 1f >= x)
			|| !(self.position.Y / 16f - reachY <= y) || !((self.position.Y + self.height) / 16f + reachY - 2f >= y))
			return;

		if (!WorldGen.InWorld(x, y, 1))
			return;

		Tile tile = Main.tile[x, y];
		bool block = tile.HasTile;
		bool wall = tile.WallType != WallID.None;

		if (!block && !wall)
			return;

		self.cursorItemIconEnabled = true;

		if (!self.ItemTimeIsZero || self.itemAnimation <= 0 || !self.controlUseItem)
			return;

		if (wallFirst ? wall : block) {
			tryPainting(self, x, y, wallFirst, true);

			// Painted: that use is spent.
			if (!self.ItemTimeIsZero)
				return;
		}

		if (wallFirst ? block : wall)
			tryPainting(self, x, y, !wallFirst, true);
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

			if (mode == ScrapeMode.CoatingsOnly)
				ScrapeCoatings(self, x, y);
			else
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
	/// Strips Illuminant or Echo and leaves the paint under it: the block's coating if it has one, else
	/// the wall's - the same order vanilla scrapes in. Vanilla's scraper always takes both together.
	/// </summary>
	private static void ScrapeCoatings(Player player, int x, int y)
	{
		if (!WorldGen.InWorld(x, y, 1))
			return;

		Tile tile = Main.tile[x, y];
		bool blockCoated = tile.HasTile && (tile.IsTileInvisible || tile.IsTileFullbright);
		bool wallCoated = tile.WallType != WallID.None && (tile.IsWallInvisible || tile.IsWallFullbright);

		if (!blockCoated && !wallCoated)
			return;

		player.cursorItemIconEnabled = true;

		if (!player.ItemTimeIsZero || player.itemAnimation <= 0 || !player.controlUseItem)
			return;

		if (blockCoated) {
			if (WorldGen.paintCoatTile(x, y, PaintCoatingID.None, broadcast: true))
				player.ApplyItemTime(player.inventory[player.selectedItem], player.tileSpeed);
		}
		else if (WorldGen.paintCoatWall(x, y, PaintCoatingID.None, broadcast: true)) {
			player.ApplyItemTime(player.inventory[player.selectedItem], player.wallSpeed);
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
	/// <param name="player">The player painting; their choice and inventory decide it.</param>
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

		return PreferCoating(player, player.inventory[paintSlot], player.inventory[coatingSlot], config)
			? coatingSlot : paintSlot;
	}

	/// <summary>
	/// Vanilla applies one item per action, so with both picked prefer whichever the target is missing:
	/// holding the mouse on a tile then lands the coating and the colour rather than stalling.
	/// </summary>
	private static bool PreferCoating(Player player, Item paint, Item coating, PaintWheelConfig config)
	{
		if (!config.Advanced.SmartCoating)
			return true;

		// The tile and half being painted when TryPainting is running; otherwise - the inventory swap
		// choosing before the use, the cursor preview - the target under the cursor, and the half the
		// next use will reach.
		int x = painting?.X ?? Player.tileTargetX;
		int y = painting?.Y ?? Player.tileTargetY;
		if (!WorldGen.InWorld(x, y, 1))
			return true;

		Tile tile = Main.tile[x, y];
		bool wall = painting?.Wall ?? HalfToPaint(player, tile, paint, coating);

		return !Coated(tile, wall, coating);
	}

	private static bool Coated(Tile tile, bool wall, Item coating) => coating.paintCoating switch {
		PaintCoatingID.Glow => wall ? tile.IsWallFullbright : tile.IsTileFullbright,
		PaintCoatingID.Echo => wall ? tile.IsWallInvisible : tile.IsTileInvisible,
		_ => true,
	};

	/// <summary>
	/// Which half the next use paints, judged before it happens: the tool's own - unless paint-both is
	/// on and that half is missing or already has both picks, when <see cref="PaintEitherHalf"/> goes on
	/// to the other. Without this the inventory swap would pick for the wrong half, and paint-both would
	/// keep offering the coating to a wall whose block was never there.
	/// </summary>
	private static bool HalfToPaint(Player player, Tile tile, Item paint, Item coating)
	{
		bool own = PaintToolSet.PaintsWalls(player.HeldItem);
		if (!PaintToolSet.IsBrushOrRoller(player.HeldItem.type) || !PaintsBoth(player))
			return own;

		bool ownThere = own ? tile.WallType != WallID.None : tile.HasTile;
		bool otherThere = own ? tile.HasTile : tile.WallType != WallID.None;
		bool ownDone = (own ? tile.WallColor : tile.TileColor) == paint.paint && Coated(tile, own, coating);

		return otherThere && (!ownThere || ownDone) ? !own : own;
	}
}
