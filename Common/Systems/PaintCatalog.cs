using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PaintWheel.Common.Systems;

/// <summary>
/// Every item that paints something, discovered from <see cref="ContentSamples"/> rather than
/// hardcoded, so modded paints and coatings appear for free.
/// </summary>
public class PaintCatalog : ModSystem
{
	private static readonly List<int> paints = new();
	private static readonly List<int> coatings = new();
	private static readonly Dictionary<byte, int> itemByPaintId = new();
	private static readonly Dictionary<int, Color> accents = new();

	/// <summary>Paint item types, ordered by <see cref="PaintID"/> so the ring reads red -> grey.</summary>
	public static IReadOnlyList<int> Paints => paints;

	/// <summary>Coating item types (Illuminant, Echo, plus any modded ones), ordered by coating id.</summary>
	public static IReadOnlyList<int> Coatings => coatings;

	public override void PostSetupContent()
	{
		paints.Clear();
		coatings.Clear();
		itemByPaintId.Clear();
		accents.Clear();

		foreach ((int type, Item sample) in ContentSamples.ItemsByType) {
			if (type <= 0 || sample is null || sample.IsAir)
				continue;

			if (sample.paint > 0) {
				paints.Add(type);
				// First item wins if two claim the same paint id - vanilla types are enumerated first.
				itemByPaintId.TryAdd(sample.paint, type);
			}
			else if (sample.paintCoating > 0) {
				coatings.Add(type);
			}
		}

		paints.Sort(ByPaintId);
		coatings.Sort(static (a, b) => CoatingIdOf(a).CompareTo(CoatingIdOf(b)));
	}

	public override void Unload()
	{
		paints.Clear();
		coatings.Clear();
		itemByPaintId.Clear();
		accents.Clear();
	}

	/// <summary>Orders paint items by paint id, which is the order the Painter sells them in: red to grey.</summary>
	public static readonly Comparison<int> ByPaintId = static (a, b) => PaintIdOf(a).CompareTo(PaintIdOf(b));

	public static byte PaintIdOf(int itemType)
		=> IsValidType(itemType) ? ContentSamples.ItemsByType[itemType].paint : (byte)0;

	public static byte CoatingIdOf(int itemType)
		=> IsValidType(itemType) ? ContentSamples.ItemsByType[itemType].paintCoating : (byte)0;

	public static bool IsPaint(int itemType) => PaintIdOf(itemType) > 0;

	/// <summary>The paint item that produces a given tile/wall paint id, for the eyedropper.</summary>
	public static int ItemForPaintId(byte paintId)
		=> itemByPaintId.TryGetValue(paintId, out int type) ? type : 0;

	/// <summary>
	/// A paint's colour. <see cref="WorldGen.paintColor"/> with one correction: it returns identical RGB
	/// for a hue and its Deep variant (ids 1-12 and 13-24 share branches), so the deep half is darkened.
	/// </summary>
	public static Color AccentColor(int itemType)
	{
		// Cached: a paint's colour never changes, and this is read for every swatch every frame.
		if (accents.TryGetValue(itemType, out Color cached))
			return cached;

		Color accent = ComputeAccent(itemType);
		accents[itemType] = accent;

		return accent;
	}

	private static Color ComputeAccent(int itemType)
	{
		if (!IsValidType(itemType))
			return Color.White;

		Item sample = ContentSamples.ItemsByType[itemType];

		if (sample.paintCoating > 0) {
			Color coating = WorldGen.coatingColor(sample.paintCoating);
			coating.A = 255;
			return coating;
		}

		byte paintId = sample.paint;
		Color color = WorldGen.paintColor(paintId);
		color.A = 255;

		if (paintId >= PaintID.DeepRedPaint && paintId <= PaintID.DeepPinkPaint)
			color = new Color((int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));

		return color;
	}

	private static bool IsValidType(int itemType)
		=> itemType > 0 && ContentSamples.ItemsByType.ContainsKey(itemType);
}
