using System;
using Microsoft.Xna.Framework;

namespace PaintWheel.Common.UI.Picker;

/// <summary>The wheel's angle and ring maths, the supply fade and the open easing.</summary>
internal static class WheelMath
{
	/// <summary>
	/// Which sector the cursor points at, or -1 inside the dead zone. By angle, not by hover, so a flick
	/// in a direction is enough. Sector 0 is centred straight up; they run clockwise.
	/// </summary>
	public static int SectorAt(Vector2 center, Vector2 cursor, int count, float deadZone)
	{
		if (count <= 0)
			return -1;

		Vector2 delta = cursor - center;
		if (delta.LengthSquared() < deadZone * deadZone)
			return -1;

		float angle = MathF.Atan2(delta.Y, delta.X) + MathHelper.PiOver2;
		if (angle < 0f)
			angle += MathHelper.TwoPi;

		float slice = MathHelper.TwoPi / count;
		int sector = (int)MathF.Floor((angle + slice * 0.5f) % MathHelper.TwoPi / slice);

		return Math.Clamp(sector, 0, count - 1);
	}

	/// <summary>Where swatch <paramref name="index"/> sits, matching <see cref="SectorAt"/>'s geometry.</summary>
	public static Vector2 SectorPosition(Vector2 center, int index, int count, float radius)
	{
		if (count <= 0)
			return center;

		float angle = index * MathHelper.TwoPi / count;
		return center + new Vector2(MathF.Sin(angle) * radius, -MathF.Cos(angle) * radius);
	}

	/// <summary>
	/// How solid a swatch looks. Not linear on the stack: paint stacks to 999, so a linear map renders
	/// 400 paint at 40%. Full until the stack drops below <paramref name="fadeStart"/>, then a ramp.
	/// </summary>
	public static float SwatchAlpha(int total, int fadeStart, float minAlpha)
	{
		if (total <= 0)
			return 0f;

		if (fadeStart <= 0 || total >= fadeStart)
			return 1f;

		return MathHelper.Lerp(minAlpha, 1f, total / (float)fadeStart);
	}

	/// <summary>Ease-out cubic, so the wheel snaps out and settles instead of drifting.</summary>
	public static float EaseOut(float t)
	{
		t = MathHelper.Clamp(t, 0f, 1f);
		float inverse = 1f - t;
		return 1f - inverse * inverse * inverse;
	}
}
