using Terraria.ModLoader;

namespace PaintWheel;

/// <summary>
/// The mod itself, which loads nothing directly: keybinds, detours and the language override each
/// register in their own system under Common/Systems, the picker hooks in through
/// Common/UI/PaintWheelUISystem, and tModLoader finds the players, global items and config by itself.
/// This only keeps the instance around for logging from static helpers.
/// </summary>
public class PaintWheel : Mod
{
	/// <summary>The loaded mod instance, for logging from static helpers.</summary>
	public static PaintWheel Instance { get; private set; }

	public override void Load() => Instance = this;

	public override void Unload() => Instance = null;
}
