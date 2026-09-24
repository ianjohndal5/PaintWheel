using PaintWheel.Common.Painting;
using Terraria;

namespace PaintWheel.Common.Players;

/// <summary>
/// Not a ModPlayer: the static shortcut the picker and the detours use to reach the local player's
/// choices. The data itself lives on <see cref="PaintWheelPlayer"/> so it saves with the character and
/// can never leak between players.
/// </summary>
public static class PaintSelection
{
	/// <summary>
	/// Chosen paint meaning "place it bare". Distinct from 0, which means no opinion at all and lets
	/// vanilla scan the inventory - the thing this choice exists to stop.
	/// </summary>
	public const int NoPaint = -1;

	/// <summary>The selection state of a specific player, or null if their ModPlayer is not ready.</summary>
	public static PaintWheelPlayer StateOf(Player player)
		=> player is not null && player.TryGetModPlayer(out PaintWheelPlayer state) ? state : null;

	/// <summary>The local player's selection state, or null outside of a live world.</summary>
	public static PaintWheelPlayer Local
	{
		get {
			if (Main.gameMenu || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers)
				return null;

			Player player = Main.LocalPlayer;
			return player is not null && player.active ? StateOf(player) : null;
		}
	}

	public static int Paint => Local?.SelectedPaint ?? 0;

	public static int Coating => Local?.SelectedCoating ?? 0;

	/// <summary>Key of the palette the picker should open on. Empty means the auto-filled one.</summary>
	public static string Palette => Local?.ActivePalette ?? "";

	public static void SetPalette(string key) => Local?.SetActivePalette(key);

	/// <summary>Page of <see cref="Palette"/> the picker should open on.</summary>
	public static int Page => Local?.ActivePage ?? 0;

	public static void SetPage(int page) => Local?.SetActivePage(page);

	/// <summary>Scrape mode, or <see cref="ScrapeMode.Off"/> when the picker is choosing colours.</summary>
	public static ScrapeMode Scrape => Local?.Scrape ?? ScrapeMode.Off;

	public static bool EnterScrape() => Local?.EnterScrapeMode() ?? false;

	public static void SetScrapeTarget(ScrapeMode mode) => Local?.SetScrapeTarget(mode);

	public static void ExitScrape() => Local?.ExitScrapeMode();

	public static void SelectPaint(int itemType) => Local?.SelectPaint(itemType);

	public static void ToggleNoPaint() => Local?.ToggleNoPaint();

	public static void SelectCoating(int itemType) => Local?.SelectCoating(itemType);

	/// <summary>Whether the brush and roller each reach both halves of a tile.</summary>
	public static bool PaintBoth => Local?.PaintBoth ?? false;

	public static void TogglePaintBoth() => Local?.TogglePaintBoth();
}
