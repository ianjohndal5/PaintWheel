using System.Collections.Generic;

namespace PaintWheel.Common.UI.Picker;

/// <summary>A palette: a named set of paints, shown one page at a time.</summary>
internal sealed class Palette
{
	/// <summary>Stable identity, for remembering the choice across sessions and languages.</summary>
	public string Key = "";

	public string Name = "";

	/// <summary>
	/// Which config preset this palette is, or -1 for the automatic one. Carried rather than looked
	/// up by name: two presets can share a name, and matching on it would edit or delete the wrong
	/// one. The key stays a name because it has to survive reordering and a restart.
	/// </summary>
	public int Preset = -1;

	public readonly List<int> Paints = new();
}
