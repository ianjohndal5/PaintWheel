using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PaintWheel.Common.Configs;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// The swatches turning to another page or palette. On the wheel the ring turns round the way you
/// went, the page left behind carrying on out and fading as the new one turns in to its place; the bar
/// slides its bands in from that side, and the grid fades its in. So a change of page reads as the
/// picker moving on, not as the colours being swapped under the cursor. Timed off the open animation,
/// and off along with it.
/// </summary>
internal static class PageTurn
{
	/// <summary>How far round the ring turns over a page, in radians.</summary>
	internal const float Sweep = MathHelper.Pi / 4f;

	/// <summary>How far the leaving page drifts out, and the arriving one comes in from, as a share of the radius.</summary>
	internal const float Spread = 0.12f;

	/// <summary>0 as the page turns, 1 once it has settled.</summary>
	internal static float Progress { get; private set; } = 1f;

	/// <summary>Which way it turns: 1 on - clockwise - and -1 back.</summary>
	internal static int Direction { get; private set; } = 1;

	internal static bool Turning => Progress < 1f;

	/// <summary>How far through, eased: quick to start, gentle to land, as the wheel springs open.</summary>
	internal static float Eased => WheelMath.EaseOut(Progress);

	/// <summary>The page being turned away from, and its stacks, drawn on their way out under the new one.</summary>
	internal static readonly List<int> Leaving = new();

	internal static readonly List<int> LeavingStacks = new();

	/// <summary>
	/// Makes a change of page - <paramref name="change"/>, false when there was nothing to change to -
	/// and turns to the result, remembering the page it leaves first.
	/// </summary>
	internal static bool Turn(int direction, PaintWheelConfig config, Func<bool> change)
	{
		var leaving = new List<int>(PickerContent.Swatches);
		var stacks = new List<int>(PickerContent.SwatchStacks);

		if (!change())
			return false;

		if (config.Appearance.OpenAnimationTicks <= 0) {
			Settle();
			return true;
		}

		Leaving.Clear();
		Leaving.AddRange(leaving);
		LeavingStacks.Clear();
		LeavingStacks.AddRange(stacks);

		Direction = direction >= 0 ? 1 : -1;
		Progress = 0f;
		return true;
	}

	/// <summary>Run every update. Half as long again as the wheel takes to open: a turn has further to go.</summary>
	internal static void Update(PaintWheelConfig config)
	{
		if (!Turning)
			return;

		Progress = MathF.Min(1f, Progress + 1f / (Math.Max(1, config.Appearance.OpenAnimationTicks) * 1.5f));

		if (!Turning)
			Settle();
	}

	/// <summary>Ends a turn where it is, for when the picker is put away or reopened.</summary>
	internal static void Settle()
	{
		Progress = 1f;
		Leaving.Clear();
		LeavingStacks.Clear();
	}
}
