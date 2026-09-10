namespace PaintWheel.Common;

/// <summary>
/// What the picker is doing. Off means it picks colours; anything else means it is in scrape mode and
/// says which half of a tile the scraper is allowed to strip.
/// </summary>
public enum ScrapeMode
{
	/// <summary>Not scraping. The picker shows paints.</summary>
	Off,

	/// <summary>Vanilla behaviour: the block's paint if it has any, otherwise the wall's.</summary>
	BlocksAndWalls,

	/// <summary>Only block paint. Walls are left alone even when the block has nothing to strip.</summary>
	BlocksOnly,

	/// <summary>Only wall paint, even when the block in front of it is painted.</summary>
	WallsOnly,
}
