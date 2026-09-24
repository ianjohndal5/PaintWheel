using System;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// Splitting a palette into pages. Pure and separate because the failure is silent: an off-by-one here
/// does not throw, it just makes the last colour unreachable.
/// </summary>
internal static class WheelPaging
{
	/// <summary>How many pages a palette of <paramref name="total"/> paints needs.</summary>
	public static int PageCount(int total, int perPage)
	{
		if (total <= 0 || perPage <= 0)
			return 0;

		return (total + perPage - 1) / perPage;
	}

	/// <summary>One page's range. Together the pages cover every index exactly once, whatever the sizes.</summary>
	public static void PageSlice(int total, int perPage, int page, out int start, out int count)
	{
		start = 0;
		count = 0;

		int pages = PageCount(total, perPage);
		if (pages <= 0 || page < 0 || page >= pages)
			return;

		start = page * perPage;
		count = Math.Min(perPage, total - start);
	}
}
