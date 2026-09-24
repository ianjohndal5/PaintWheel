using Microsoft.Xna.Framework;

namespace PaintWheel.Common.UI;

/// <summary>
/// Colours that mean the same thing wherever they appear - the picker, the config's palette grid, the
/// cursor marker, tooltips and the text that floats up - kept in one place so they cannot drift apart.
/// </summary>
public static class UIColors
{
	/// <summary>Gold: the one that is chosen - the paint in use, a palette's members, a picked slot.</summary>
	public static readonly Color Chosen = new(255, 216, 122);

	/// <summary>The dark edge around a colour cell, so neighbouring colours do not bleed together.</summary>
	public static readonly Color CellRim = new(20, 20, 30);
}
