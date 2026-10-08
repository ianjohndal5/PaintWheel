using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PaintWheel.Common.Configs;
using PaintWheel.Common.Painting;
using PaintWheel.Common.Players;
using PaintWheel.Common.Systems;
using ReLogic.Localization.IME;
using ReLogic.OS;
using Terraria;
using Terraria.GameInput;
using Terraria.Localization;

namespace PaintWheel.Common.UI.Picker;

/// <summary>
/// The palette board, opened from the middle of the wheel. Along the top, a pill for each palette -
/// the automatic one first, then every saved one - picks the palette in use; the green bar under them
/// slides the row along when there are more than four. The grid below is every paint in the game, the
/// palette's own in the gold frame: clicking one adds it or takes it out, saved at once. Down the right
/// side: back to the colours, a new palette, delete; the brush in the corner renames. Drawn from the
/// art in Assets/Textures/Palette as pixel art, scaled with no smoothing.
/// <para/>
/// The pills list the saved palettes as the config holds them, empty ones included - a palette with
/// nothing in it has no place on the wheel, but it has to be on the board to be filled. The data side,
/// what is in a palette and saving it, is <see cref="PaletteEditor"/>'s.
/// </summary>
internal static class PaletteBoard
{
	// ---- Layout -----------------------------------------------------------------------------
	// In the board art's own pixels: the board is 230 by 94, and everything sits where its art has room.

	/// <summary>Screen pixels to one of the art's at the size setting's 100%. Whole, so the art is crisp there.</summary>
	private const float BaseScale = 2f;

	/// <summary>
	/// The board, pills, bar, buttons and brush, from the size setting - set each time the board is
	/// placed. The paint slots are drawn smaller, as in the design.
	/// </summary>
	private static float Scale { get; set; } = BaseScale;

	/// <summary>Three quarters of the board's: the size the design gives the paints' frames.</summary>
	private static float SlotScale => Scale * 0.75f;

	private const int BoardWidth = 230;
	private const int BoardHeight = 94;

	/// <summary>Four pills of 46, end to end, fill the bar's well.</summary>
	private const int PillsShown = 4;
	private const float PillLeft = 17f;
	private const float PillTop = 21f;
	private const float PillPitch = 46f;
	private const int PillWidth = 46;
	private const int PillHeight = 11;

	/// <summary>The inside of the well the pills slide along. A pill partway out is cut off at its edges.</summary>
	private static readonly Rectangle PillWell = new(16, 20, 189, 12);

	private const float TrackLeft = 18f;
	private const float TrackTop = 34f;
	private const float TrackLength = 178f;
	private const int ThumbWidth = 27;
	private const int ThumbHeight = 8;

	/// <summary>Ten to a row and two rows on show, the size of the board's lower well.</summary>
	private const int Columns = 10;
	private const int RowsShown = 2;
	private const float SlotPitchX = 17f;
	private const float SlotPitchY = 16f;
	private const float FirstSlotX = 27f;
	private const float FirstSlotY = 53.5f;

	/// <summary>Where the colour shows through each frame: an 11 pixel hole, ringed by the frame's own pixels.</summary>
	private static readonly Rectangle UsedHole = new(6, 5, 11, 11);
	private static readonly Rectangle UnusedHole = new(4, 3, 11, 11);

	/// <summary>Hung off the plank on the right, sticking out past it, with the plank showing between them.</summary>
	private static readonly Vector2 ExitAt = new(211f, 7f);
	private static readonly Vector2 AddAt = new(211f, 22f);
	private static readonly Vector2 DeleteAt = new(211f, 37f);
	private static readonly Vector2 RenameAt = new(187f, 70f);
	/// <summary>Top left of each 7 by 6 arrow beside the paints, on whole pixels of the board.</summary>
	private static readonly Vector2 RowsUpAt = new(193f, 47f);
	private static readonly Vector2 RowsDownAt = new(193f, 61f);

	private const float InfoGap = 16f;
	/// <summary>
	/// A pill's name, in font scale per screen pixel of the art's, so it keeps its size on the pill -
	/// down to a floor, below which a name is shortened rather than shrunk past reading.
	/// </summary>
	private static float PillTextScale => Math.Max(0.4f, 0.272f * Scale);

	private static float PillTextMinScale => Math.Max(0.35f, 0.2f * Scale);

	// ---- State ------------------------------------------------------------------------------

	/// <summary>What the cursor can be on.</summary>
	internal enum Part
	{
		None,
		Board,
		Pill,
		Track,
		Thumb,
		Slot,
		Exit,
		Add,
		Delete,
		Rename,
		RowsUp,
		RowsDown,
	}

	/// <summary>The saved palette on show, or -1 for the automatic one.</summary>
	internal static int Shown = -1;

	internal static Part Hovered;

	/// <summary>Which pill or slot, for those parts.</summary>
	internal static int HoveredIndex = -1;

	/// <summary>The saved palette whose delete has been clicked once, or -1. A second click deletes it.</summary>
	internal static int ArmedDelete = -1;

	/// <summary>Top left of the board on screen, in UI space.</summary>
	private static Vector2 origin;

	/// <summary>How far along the row of pills is slid, in the board's pixels, and where it is gliding to.</summary>
	private static float pillScroll;
	private static float pillTarget;

	private static int firstRow;

	private static bool dragging;
	private static float dragGrab;

	/// <summary>Where the hover was last tested: the mouse, or the right stick's cursor on a gamepad.</summary>
	private static Vector2 lastCursor;

	/// <summary>Pills in the row: the automatic palette, then each saved one.</summary>
	private static int EntryCount(PaintWheelConfig config) => 1 + config.Presets.Count;

	private static int EntryOf(int preset) => preset + 1;

	private static int PresetOf(int entry) => entry - 1;

	private static int Rows => (PaletteEditor.GridPaints.Count + Columns - 1) / Columns;

	// ---- Opening and following the palette in use ------------------------------------------

	/// <summary>Puts the board up on the palette in use.</summary>
	internal static void Open(PaintWheelConfig config)
	{
		Reset();
		Show(PickerContent.ActivePalette?.Preset ?? -1, config);

		// Already there when it opens, rather than gliding in from the start of the row.
		pillScroll = pillTarget;
	}

	/// <summary>Forgets everything about the board, for when the picker is put away.</summary>
	internal static void Reset()
	{
		StopRenaming();
		Shown = -1;
		Hovered = Part.None;
		HoveredIndex = -1;
		ArmedDelete = -1;
		pillScroll = pillTarget = 0f;
		firstRow = 0;
		dragging = false;
	}

	internal static void ClearHover()
	{
		Hovered = Part.None;
		HoveredIndex = -1;
		ArmedDelete = -1;
	}

	/// <summary>Moves the board onto the palette now in use - after a palette key, say.</summary>
	internal static void FollowActive(PaintWheelConfig config) => Show(PickerContent.ActivePalette?.Preset ?? -1, config);

	/// <summary>
	/// Shows a palette on the board, and makes it the one the wheel uses when it has anything to use. A
	/// palette with nothing in it is shown all the same, to be filled.
	/// </summary>
	private static void Show(int preset, PaintWheelConfig config)
	{
		if (preset >= config.Presets.Count)
			preset = -1;

		if (preset != Shown) {
			StopRenaming();

			// Moving off a palette "+" just made, still empty, throws it away, as leaving the board does.
			int dropped = PaletteEditor.DiscardIfEmptyNew(config);
			if (dropped >= 0) {
				if (preset > dropped)
					preset--;

				PickerContent.RebuildPalettes(config);
				PickerContent.ApplyPage(config);
			}
		}

		Shown = preset;
		ArmedDelete = -1;

		if (preset >= 0)
			PaletteEditor.Begin(preset, config);
		else
			PaletteEditor.ShowAutomatic();

		PickerContent.SelectPreset(preset, config);
		RevealPill(EntryOf(preset), config);
		firstRow = Math.Clamp(firstRow, 0, Math.Max(0, Rows - RowsShown));
	}

	/// <summary>Number key N picks the Nth palette, the automatic one first. False when there is no such palette.</summary>
	internal static bool SelectEntry(int entry, PaintWheelConfig config)
	{
		if (entry < 0 || entry >= EntryCount(config))
			return false;

		Show(PresetOf(entry), config);
		return true;
	}

	/// <summary>Slides the row just far enough that the pill is wholly in view.</summary>
	private static void RevealPill(int entry, PaintWheelConfig config)
	{
		float left = entry * PillPitch;

		if (left < pillTarget)
			pillTarget = left;
		else if (left + PillWidth > pillTarget + PillsShown * PillPitch)
			pillTarget = left + PillWidth - PillsShown * PillPitch;

		pillTarget = Math.Clamp(pillTarget, 0f, MaxPillScroll(config));
	}

	/// <summary>As far as the row slides: its last pill's right end at the well's.</summary>
	private static float MaxPillScroll(PaintWheelConfig config) => Math.Max(0f, (EntryCount(config) - PillsShown) * PillPitch);

	/// <summary>Where a pill's left end is, in the board's pixels, with the row where it is now.</summary>
	private static float PillX(int entry) => PillLeft + entry * PillPitch - pillScroll;

	/// <summary>The pills at least partly in the well: the first, and one past the last.</summary>
	private static (int first, int end) PillsInView(PaintWheelConfig config)
	{
		int first = Math.Max(0, (int)MathF.Floor((pillScroll - (PillLeft - PillWell.Left)) / PillPitch));
		int end = (int)MathF.Ceiling((pillScroll + PillWell.Right - PillLeft) / PillPitch);

		// Blanks fill the well when there are fewer palettes than it holds.
		return (first, Math.Min(end, Math.Max(EntryCount(config), PillsShown)));
	}

	// ---- Geometry ---------------------------------------------------------------------------

	/// <summary>
	/// Sizes the board from the setting and centres it on the picker, kept on screen. Whole pixels, so
	/// its pixel art stays crisp.
	/// </summary>
	internal static void Place(in WheelLayout.Geometry geometry, PaintWheelConfig config)
	{
		Scale = BaseScale * Math.Clamp(config.Appearance.PaletteBoardSize, 50, 150) / 100f;

		float width = BoardWidth * Scale;
		float height = BoardHeight * Scale;

		float x = Main.screenWidth > width + 8f
			? MathHelper.Clamp(geometry.MenuCenter.X - width * 0.5f, 4f, Main.screenWidth - width - 4f)
			: (Main.screenWidth - width) * 0.5f;

		float y = Main.screenHeight > height + 8f
			? MathHelper.Clamp(geometry.MenuCenter.Y - height * 0.5f, 4f, Main.screenHeight - height - 4f)
			: (Main.screenHeight - height) * 0.5f;

		origin = new Vector2(MathF.Round(x), MathF.Round(y));
	}

	/// <summary>The whole board on screen, for the gamepad's stick to range over.</summary>
	internal static Rectangle Bounds
		=> new((int)origin.X, (int)origin.Y, (int)(BoardWidth * Scale), (int)(BoardHeight * Scale));

	private static Vector2 At(float x, float y) => origin + new Vector2(x, y) * Scale;

	private static Vector2 At(Vector2 point) => At(point.X, point.Y);

	private static Vector2 SlotCenter(int shown) => At(FirstSlotX + shown % Columns * SlotPitchX, FirstSlotY + shown / Columns * SlotPitchY);

	/// <summary>The thumb's left end: as far along the track as the row is along its slide.</summary>
	private static float ThumbLeft(PaintWheelConfig config)
	{
		float max = MaxPillScroll(config);
		return TrackLeft + (max <= 0f ? 0f : pillScroll / max * (TrackLength - ThumbWidth));
	}

	/// <summary>Slides the row to match the thumb held at <paramref name="cursorX"/>, straight away: it is in hand.</summary>
	private static void DragThumb(float cursorX, PaintWheelConfig config)
	{
		float max = MaxPillScroll(config);
		float along = ((cursorX - origin.X) / Scale - dragGrab - TrackLeft) / (TrackLength - ThumbWidth);
		pillScroll = pillTarget = max <= 0f ? 0f : Math.Clamp(along, 0f, 1f) * max;
	}

	private static bool Inside(Vector2 point, float left, float top, float width, float height)
		=> point.X >= left && point.X < left + width && point.Y >= top && point.Y < top + height;

	/// <summary>What is under <paramref name="cursor"/>, worked out in the board's own pixels.</summary>
	private static (Part part, int index) HitTest(Vector2 cursor, PaintWheelConfig config)
	{
		Vector2 p = (cursor - origin) / Scale;

		if (!Inside(p, 0f, 0f, BoardWidth, BoardHeight))
			return (Part.None, -1);

		if (Inside(p, ExitAt.X, ExitAt.Y, 15f, 14f))
			return (Part.Exit, -1);

		if (Inside(p, AddAt.X, AddAt.Y, 15f, 14f))
			return (Part.Add, -1);

		if (Inside(p, DeleteAt.X, DeleteAt.Y, 17f, 15f))
			return (Part.Delete, -1);

		if (Inside(p, PillWell.Left, PillWell.Top, PillWell.Width, PillWell.Height)) {
			(int first, int end) = PillsInView(config);
			for (int entry = first; entry < Math.Min(end, EntryCount(config)); entry++) {
				if (Inside(p, PillX(entry), PillTop, PillWidth, PillHeight))
					return (Part.Pill, entry);
			}
		}

		if (Inside(p, ThumbLeft(config), TrackTop, ThumbWidth, ThumbHeight))
			return (Part.Thumb, -1);

		if (Inside(p, TrackLeft, TrackTop - 1f, TrackLength, ThumbHeight + 2f))
			return (Part.Track, -1);

		if (Rows > RowsShown) {
			// A little wider than the arrows themselves, which are small to hit.
			if (Inside(p, RowsUpAt.X - 2f, RowsUpAt.Y - 2f, 11f, 10f))
				return (Part.RowsUp, -1);

			if (Inside(p, RowsDownAt.X - 2f, RowsDownAt.Y - 2f, 11f, 10f))
				return (Part.RowsDown, -1);
		}

		float small = SlotScale / Scale;
		float halfWidth = 19f * small * 0.5f;
		float halfHeight = 17f * small * 0.5f;

		for (int shown = 0; shown < Columns * RowsShown; shown++) {
			int index = firstRow * Columns + shown;
			if (index >= PaletteEditor.GridPaints.Count)
				break;

			float x = FirstSlotX + shown % Columns * SlotPitchX;
			float y = FirstSlotY + shown / Columns * SlotPitchY;

			if (Inside(p, x - halfWidth, y - halfHeight, halfWidth * 2f, halfHeight * 2f))
				return (Part.Slot, index);
		}

		// After the paints: the brush's box takes in a corner of the last one in a full row.
		if (Inside(p, RenameAt.X, RenameAt.Y, 11f, 12f))
			return (Part.Rename, -1);

		return (Part.Board, -1);
	}

	// ---- Input ------------------------------------------------------------------------------

	/// <summary>Run every update while the board is up: what the cursor is on, and the scroll bar being dragged.</summary>
	internal static void UpdateHover(Vector2 cursor, PaintWheelConfig config)
	{
		lastCursor = cursor;
		Part was = Hovered;
		int wasIndex = HoveredIndex;
		(Hovered, HoveredIndex) = HitTest(cursor, config);

		// A tick on reaching a slot or a pill, as on a swatch.
		if (Hovered is Part.Slot or Part.Pill && (Hovered != was || HoveredIndex != wasIndex))
			PaintPicker.Play(Terraria.ID.SoundID.MenuTick, config);

		if (Hovered != Part.Delete)
			ArmedDelete = -1;

		if (dragging && !Main.mouseLeft)
			dragging = false;

		if (dragging) {
			DragThumb(cursor.X, config);
			return;
		}

		// Glides the rest of the way each tick, so a notch of the wheel or a jump to a pill reads as
		// the row moving rather than a cut. Clamped too, for a palette deleted while it was out of view.
		float max = MaxPillScroll(config);
		pillTarget = Math.Clamp(pillTarget, 0f, max);
		pillScroll = Math.Clamp(pillScroll + (pillTarget - pillScroll) * 0.35f, 0f, max);

		if (MathF.Abs(pillTarget - pillScroll) < 0.05f)
			pillScroll = pillTarget;
	}

	/// <summary>
	/// A left click on the board. False when it was outside the board, which the caller takes as going
	/// back to the colours.
	/// </summary>
	internal static bool Click(PaintWheelConfig config)
	{
		// A click anywhere but the name being typed keeps it, then does what it would have done.
		if (IsRenaming) {
			if (Hovered == Part.Pill && HoveredIndex == EntryOf(renaming))
				return true;

			CommitRename(config);

			// The brush again keeps the name, as Enter does, rather than opening the field straight back up.
			if (Hovered == Part.Rename) {
				PaintPicker.Play(Terraria.ID.SoundID.MenuTick, config);
				return true;
			}
		}

		switch (Hovered) {
			case Part.None:
				return false;

			case Part.Pill:
				Show(PresetOf(HoveredIndex), config);
				PaintPicker.Play(Terraria.ID.SoundID.Grab, config);
				return true;

			case Part.Slot:
				ToggleSlot(HoveredIndex, config);
				return true;

			case Part.Thumb:
				dragging = true;
				dragGrab = (lastCursor.X - origin.X) / Scale - ThumbLeft(config);
				return true;

			case Part.Track:
				// Like any slider: the thumb comes to the click, held by its middle, to drag on from there.
				dragging = true;
				dragGrab = ThumbWidth * 0.5f;
				DragThumb(lastCursor.X, config);
				PaintPicker.Play(Terraria.ID.SoundID.MenuTick, config);
				return true;

			case Part.RowsUp or Part.RowsDown:
				if (ScrollRows(Hovered == Part.RowsUp ? -1 : 1))
					PaintPicker.Play(Terraria.ID.SoundID.MenuTick, config);
				return true;

			case Part.Exit:
				PaintPicker.CloseBoard();
				PaintPicker.Play(Terraria.ID.SoundID.MenuClose, config);
				return true;

			case Part.Add: {
				// An empty one "+" made a moment ago goes first, or "+" twice would leave a blank behind.
				PaletteEditor.DiscardIfEmptyNew(config);

				// Shown again even when the new one could not be saved, since the empty one may be gone.
				int preset = PaletteEditor.CreatePreset(config);
				Show(preset >= 0 ? preset : Shown, config);

				PaintPicker.Play(preset >= 0 ? Terraria.ID.SoundID.Grab : Terraria.ID.SoundID.MenuClose, config);
				return true;
			}

			case Part.Delete:
				Delete(config);
				return true;

			case Part.Rename:
				PaintPicker.Play(StartRenaming(config) ? Terraria.ID.SoundID.MenuTick : Terraria.ID.SoundID.MenuClose, config);
				return true;

			default:
				return true;
		}
	}

	private static void ToggleSlot(int index, PaintWheelConfig config)
	{
		if (index < 0 || index >= PaletteEditor.GridPaints.Count) {
			PaintPicker.Play(Terraria.ID.SoundID.MenuClose, config);
			return;
		}

		int type = PaletteEditor.GridPaints[index];

		if (Shown >= 0) {
			PaletteEditor.ToggleMember(type, config);
		}
		else {
			// The automatic palette keeps its changes with the character. It keeps a paint at least, too:
			// empty, the picker it is reached from would not open.
			bool member = PaletteEditor.Members.Contains(type);
			if (PaintSelection.Local is not PaintWheelPlayer state || (member && PaletteEditor.Members.Count <= 1)) {
				PaintPicker.Play(Terraria.ID.SoundID.MenuClose, config);
				return;
			}

			state.SetInAutoPalette(type, !member);
		}

		// The wheel follows at once: a palette that just got its first colour is now one it can use.
		PickerContent.RebuildPalettes(config);
		PickerContent.SelectPreset(Shown, config);
		PickerContent.ApplyPage(config);
		PickerContent.RefreshStacks();

		if (Shown < 0)
			PaletteEditor.ShowAutomatic();

		PaintPicker.Play(Terraria.ID.SoundID.MenuTick, config);
	}

	private static void Delete(PaintWheelConfig config)
	{
		if (Shown < 0) {
			PaintPicker.Play(Terraria.ID.SoundID.MenuClose, config);
			return;
		}

		// Deleting is for good, so it takes a second click on the same button.
		if (ArmedDelete != Shown) {
			ArmedDelete = Shown;
			PaintPicker.Play(Terraria.ID.SoundID.MenuTick, config);
			return;
		}

		int preset = Shown;
		ArmedDelete = -1;
		bool deleted = PaletteEditor.DeletePreset(preset, config);

		// On to the palette before it, so the row stays where it was.
		Show(Math.Min(preset, config.Presets.Count) - 1, config);
		PaintPicker.Play(deleted ? Terraria.ID.SoundID.Grab : Terraria.ID.SoundID.MenuClose, config);
	}

	/// <summary>
	/// Scroll walks whatever the cursor is over: the pills over their row and bar, the paints anywhere
	/// else. False when that is already as far as it goes.
	/// </summary>
	internal static bool Scroll(int direction, PaintWheelConfig config)
	{
		bool overPills = Hovered is Part.Pill or Part.Track or Part.Thumb;

		if (!overPills && Rows > RowsShown)
			return ScrollRows(direction);

		// Half a pill a notch, glided rather than jumped.
		float next = Math.Clamp(pillTarget + direction * PillPitch * 0.5f, 0f, MaxPillScroll(config));
		if (next == pillTarget)
			return false;

		pillTarget = next;
		return true;
	}

	private static bool ScrollRows(int direction)
	{
		int next = Math.Clamp(firstRow + direction, 0, Math.Max(0, Rows - RowsShown));
		if (next == firstRow)
			return false;

		firstRow = next;
		return true;
	}

	// ---- Renaming ---------------------------------------------------------------------------

	/// <summary>Longest name the field takes. The config's own text box has no limit, so one set there may be longer.</summary>
	private const int MaxNameLength = 40;

	/// <summary>The saved palette whose name is being typed, or -1.</summary>
	private static int renaming = -1;

	private static string renameText = "";

	internal static bool IsRenaming => renaming >= 0;

	private static bool StartRenaming(PaintWheelConfig config)
	{
		if (Shown < 0 || Shown >= config.Presets.Count)
			return false;

		renaming = Shown;
		renameText = config.Presets[Shown]?.Name ?? "";
		return true;
	}

	internal static void StopRenaming()
	{
		renaming = -1;
		renameText = "";
	}

	/// <summary>Keeps the typed name, unless it is blank, and saves.</summary>
	private static void CommitRename(PaintWheelConfig config)
	{
		int preset = renaming;
		string name = renameText.Trim();
		StopRenaming();

		if (preset < 0 || preset >= config.Presets.Count || name.Length == 0 || name == config.Presets[preset]?.Name)
			return;

		PaletteEditor.RenamePreset(preset, name, config);
	}

	/// <summary>
	/// Keeps vanilla's chat off the keyboard while a name is typed - Enter would open it, and opening
	/// it clears the input before the Enter that keeps the name is read. The game clears the claim
	/// every update, before the UI updates, so it is renewed from there.
	/// </summary>
	internal static void ClaimTextInput()
	{
		if (!IsRenaming)
			return;

		Main.CurrentInputTextTakerOverride = typeof(PaletteBoard);

		// The chat key plays its sound before it checks for that claim, so it is kept from firing at all.
		Main.chatRelease = false;
	}

	/// <summary>
	/// Reads the keyboard into the name being typed, the way tModLoader's own text fields do. Run from
	/// the draw, once a frame: WritingText is what stops the same keys moving the player or switching
	/// the hotbar, and the game clears it again every update.
	/// </summary>
	private static void ReadRenameInput()
	{
		PaintWheelConfig config = PaintWheelConfig.Instance;
		if (config is null || !PaintPicker.IsActive || PaintPicker.Overlay != PickerOverlay.Palettes) {
			StopRenaming();
			return;
		}

		PlayerInput.WritingText = true;
		Main.instance.HandleIME();

		// Too long is cut to fit rather than refused, so a paste still lands and a name already over the
		// limit can still be shortened.
		string next = Main.GetInputText(renameText);
		if (next.Length > MaxNameLength && next.Length > renameText.Length)
			next = next[..Math.Max(MaxNameLength, renameText.Length)];

		renameText = next;

		if (Main.inputTextEnter) {
			// Chat opens on an Enter it has not seen held. A draw can run before the next update, which
			// would no longer be claiming the keyboard, so the press is marked as already seen.
			Main.chatRelease = false;
			CommitRename(config);
		}
		else if (Main.inputTextEscape) {
			StopRenaming();
			PaintPicker.SwallowEscape();
		}
	}

	// ---- Drawing ----------------------------------------------------------------------------

	private static readonly Color Rest = new(222, 222, 222);
	private static readonly Color Muted = new(200, 205, 230);
	private static readonly Color Unowned = new(58, 58, 70);

	internal static void Draw(SpriteBatch spriteBatch, PaintWheelConfig config, in WheelLayout.Geometry geometry, float opacity)
	{
		if (IsRenaming)
			ReadRenameInput();

		Place(geometry, config);

		// Pixel art, so no smoothing while it is drawn - then back to the layer's own settings for the
		// text. The pills, and their names, are cut off at the edges of their well as they slide.
		WheelDrawing.RestartBatch(spriteBatch, SamplerState.PointClamp);
		DrawArt(spriteBatch, UITextures.PaletteBoard, At(0f, 0f), Scale, Color.White * opacity);

		Rectangle oldClip = spriteBatch.GraphicsDevice.ScissorRectangle;
		Rectangle clip = ScreenClip(spriteBatch.GraphicsDevice, PillWell);

		if (clip.Width > 0 && clip.Height > 0) {
			WheelDrawing.RestartBatch(spriteBatch, SamplerState.PointClamp, clip);
			DrawPills(spriteBatch, config, opacity);

			WheelDrawing.RestartBatch(spriteBatch, SamplerState.LinearClamp, clip);
			DrawPillNames(spriteBatch, config, opacity);
		}

		WheelDrawing.RestartBatch(spriteBatch, SamplerState.PointClamp);
		spriteBatch.GraphicsDevice.ScissorRectangle = oldClip;

		DrawArt(spriteBatch, UITextures.PaletteScrollThumb, At(ThumbLeft(config), TrackTop), Scale,
			(dragging || Hovered is Part.Thumb ? Color.White : Rest) * opacity);
		DrawSlots(spriteBatch, opacity);
		DrawButtons(spriteBatch, opacity);
		DrawRowArrows(spriteBatch, opacity);

		WheelDrawing.RestartBatch(spriteBatch, SamplerState.LinearClamp);
		DrawInfo(spriteBatch, config, opacity);

		// The candidates of a word being composed, for Chinese and the other languages typed through an
		// IME; drawn only while there are some.
		if (IsRenaming) {
			Rectangle bounds = Bounds;
			Main.instance.DrawWindowsIMEPanel(new Vector2(bounds.Center.X, bounds.Bottom + InfoGap + 40f), 0.5f);
		}
	}

	/// <summary>
	/// A box in the board's pixels as the screen's own, which is what clipping takes: through the
	/// interface scale, and kept inside the screen. Empty when it is off it.
	/// </summary>
	private static Rectangle ScreenClip(GraphicsDevice device, Rectangle area)
	{
		Vector2 topLeft = Vector2.Transform(At(area.Left, area.Top), Main.UIScaleMatrix);
		Vector2 bottomRight = Vector2.Transform(At(area.Right, area.Bottom), Main.UIScaleMatrix);

		var box = new Rectangle((int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y),
			(int)MathF.Round(bottomRight.X - topLeft.X), (int)MathF.Round(bottomRight.Y - topLeft.Y));

		return Rectangle.Intersect(box, device.Viewport.Bounds);
	}

	private static void DrawArt(SpriteBatch spriteBatch, Texture2D texture, Vector2 topLeft, float scale, Color color)
	{
		if (texture is not null)
			spriteBatch.Draw(texture, topLeft, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
	}

	private static void DrawPills(SpriteBatch spriteBatch, PaintWheelConfig config, float opacity)
	{
		int shownEntry = EntryOf(Shown);
		(int first, int end) = PillsInView(config);

		for (int entry = first; entry < end; entry++) {
			// Whole pixels of the art, so a sliding pill does not shimmer.
			Vector2 at = At(MathF.Round(PillX(entry) * Scale) / Scale, PillTop);
			bool real = entry < EntryCount(config);
			bool active = real && entry == shownEntry;

			DrawArt(spriteBatch, active ? UITextures.PaletteActivePill : UITextures.PaletteInactivePill, at, Scale, Color.White * opacity);

			// Hover lights an inactive pill partway toward the active one.
			if (real && !active && Hovered == Part.Pill && HoveredIndex == entry)
				DrawArt(spriteBatch, UITextures.PaletteActivePill, at, Scale, Color.White * (opacity * 0.4f));
		}
	}

	private static void DrawPillNames(SpriteBatch spriteBatch, PaintWheelConfig config, float opacity)
	{
		float room = (PillWidth - 6) * Scale;
		(int first, int end) = PillsInView(config);

		for (int entry = first; entry < Math.Min(end, EntryCount(config)); entry++) {
			Vector2 center = At(MathF.Round(PillX(entry) * Scale) / Scale + PillWidth * 0.5f, PillTop + PillHeight * 0.5f + 0.5f);
			int preset = PresetOf(entry);

			if (IsRenaming && preset == renaming) {
				DrawRenameField(spriteBatch, center, room, opacity);
				continue;
			}

			string name = PickerContent.PaletteName(preset, config);
			float scale = PillTextScale;
			float width = WheelDrawing.MeasureText(name, scale).X;

			if (width > room)
				scale = Math.Max(PillTextMinScale, scale * room / width);

			name = WheelDrawing.Truncate(name, scale, room);
			WheelDrawing.DrawTextCentered(spriteBatch, name, center, preset == Shown ? Color.White : Muted, opacity, scale);
		}
	}

	/// <summary>
	/// The name being typed, trimmed from the left so the end - where the typing is - stays in view.
	/// A word still being composed through an IME follows it in yellow, the way chat shows one.
	/// </summary>
	private static void DrawRenameField(SpriteBatch spriteBatch, Vector2 center, float room, float opacity)
	{
		string composing = Platform.Get<IImeService>().CompositionString ?? "";
		bool caret = composing.Length == 0 && Main.GameUpdateCount / 30 % 2 == 0;
		string shown = renameText + composing;

		while (shown.Length > composing.Length && WheelDrawing.MeasureText(shown + "|", PillTextScale).X > room)
			shown = shown[1..];

		string typed = shown[..(shown.Length - composing.Length)];
		float width = WheelDrawing.MeasureText(shown + "|", PillTextScale).X;
		var at = new Vector2(center.X - width * 0.5f, center.Y);

		WheelDrawing.DrawTextLeft(spriteBatch, typed + (caret ? "|" : ""), at, Color.White, opacity, PillTextScale);

		if (composing.Length > 0) {
			var after = new Vector2(at.X + WheelDrawing.MeasureText(typed, PillTextScale).X, at.Y);
			WheelDrawing.DrawTextLeft(spriteBatch, composing, after, new Color(255, 240, 20), opacity, PillTextScale);
		}
	}

	/// <summary>
	/// Each paint shows through its frame's hole: the colour is a square under the hole, and the frame's
	/// own pixels round it off and lay the shine over it. Gold frames are the palette's.
	/// </summary>
	private static void DrawSlots(SpriteBatch spriteBatch, float opacity)
	{
		Player player = Main.LocalPlayer;
		Rectangle? outline = null;

		for (int shown = 0; shown < Columns * RowsShown; shown++) {
			int index = firstRow * Columns + shown;
			if (index >= PaletteEditor.GridPaints.Count)
				break;

			int type = PaletteEditor.GridPaints[index];
			bool member = PaletteEditor.Members.Contains(type);
			bool hovered = Hovered == Part.Slot && HoveredIndex == index;

			Texture2D frame = member ? UITextures.PaletteSlotUsed : UITextures.PaletteSlotUnused;
			Rectangle hole = member ? UsedHole : UnusedHole;
			Vector2 size = member ? new Vector2(23f, 21f) : new Vector2(19f, 17f);
			Vector2 topLeft = SlotCenter(shown) - size * SlotScale * 0.5f;
			topLeft = new Vector2(MathF.Round(topLeft.X), MathF.Round(topLeft.Y));

			// Dimmed when you carry none: still worth adding, worth knowing you lack.
			Color colour = PaintCatalog.AccentColor(type);
			if (PaintInventory.TotalStack(player, type) <= 0)
				colour = Color.Lerp(colour, Unowned, 0.55f);

			if (hovered)
				colour = Color.Lerp(colour, Color.White, 0.2f);

			// Half a frame pixel over each edge of the hole, under the frame's own opaque ring, so no seam
			// opens at the uneven pixel widths three-quarter scale makes.
			var holeRect = new Rectangle(
				(int)MathF.Floor(topLeft.X + (hole.X - 0.5f) * SlotScale), (int)MathF.Floor(topLeft.Y + (hole.Y - 0.5f) * SlotScale),
				(int)MathF.Ceiling((hole.Width + 1f) * SlotScale), (int)MathF.Ceiling((hole.Height + 1f) * SlotScale));

			WheelDrawing.DrawRect(spriteBatch, holeRect, colour * opacity);
			DrawArt(spriteBatch, frame, topLeft, SlotScale, Color.White * opacity);

			if (hovered)
				outline = new Rectangle((int)topLeft.X - 2, (int)topLeft.Y - 2, (int)(size.X * SlotScale) + 4, (int)(size.Y * SlotScale) + 4);
		}

		// Last, since the frames touch: drawn with its own slot, the next one along would cover part of it.
		if (outline is Rectangle box)
			WheelDrawing.DrawRectOutline(spriteBatch, box, 2, Color.White * opacity);
	}

	private static void DrawButtons(SpriteBatch spriteBatch, float opacity)
	{
		bool saved = Shown >= 0;

		DrawButton(spriteBatch, UITextures.PaletteExit, At(ExitAt), Scale, Part.Exit, enabled: true, opacity);
		DrawButton(spriteBatch, UITextures.PaletteAdd, At(AddAt), Scale, Part.Add, enabled: true, opacity);

		// Armed: pulsing toward red, so the second click is plainly a second click.
		if (ArmedDelete >= 0 && ArmedDelete == Shown) {
			float pulse = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.25f);
			DrawArt(spriteBatch, UITextures.PaletteDelete, At(DeleteAt), Scale, Color.Lerp(Color.White, new Color(255, 120, 120), pulse) * opacity);
		}
		else {
			DrawButton(spriteBatch, UITextures.PaletteDelete, At(DeleteAt), Scale, Part.Delete, saved, opacity);
		}

		DrawButton(spriteBatch, UITextures.PaletteRename, At(RenameAt), Scale, Part.Rename, saved, opacity);
	}

	/// <summary>Full strength under the cursor, a shade down at rest, faint when it does nothing for this palette.</summary>
	private static void DrawButton(SpriteBatch spriteBatch, Texture2D texture, Vector2 at, float scale, Part part, bool enabled, float opacity)
	{
		Color color = !enabled ? Color.White * 0.35f
			: Hovered == part || (part == Part.Rename && IsRenaming) ? Color.White
			: Rest;

		DrawArt(spriteBatch, texture, at, scale, color * opacity);
	}

	/// <summary>
	/// The arrows beside the paints when there are more rows than the board shows. Lit like the buttons,
	/// and faint at the end they cannot scroll past.
	/// </summary>
	private static void DrawRowArrows(SpriteBatch spriteBatch, float opacity)
	{
		if (Rows <= RowsShown)
			return;

		DrawButton(spriteBatch, UITextures.PaletteArrowUp, At(RowsUpAt), Scale, Part.RowsUp, firstRow > 0, opacity);
		DrawButton(spriteBatch, UITextures.PaletteArrowDown, At(RowsDownAt), Scale, Part.RowsDown, firstRow < Rows - RowsShown, opacity);
	}

	/// <summary>Under the board: what the cursor is on, or what the board is for.</summary>
	private static void DrawInfo(SpriteBatch spriteBatch, PaintWheelConfig config, float opacity)
	{
		string text = InfoText(config);
		if (string.IsNullOrEmpty(text))
			return;

		Rectangle bounds = Bounds;
		WheelDrawing.DrawTextCentered(spriteBatch, text, new Vector2(bounds.Center.X, bounds.Bottom + InfoGap), Color.White, opacity, 0.9f);
	}

	private static string InfoText(PaintWheelConfig config)
	{
		if (IsRenaming)
			return Language.GetTextValue("Mods.PaintWheel.UI.RenameHint");

		switch (Hovered) {
			case Part.Exit:
				return Language.GetTextValue("Mods.PaintWheel.UI.Scrape.Exit");

			case Part.Add:
				return Language.GetTextValue(PaintInventory.OwnsAnyPaint(Main.LocalPlayer)
					? "Mods.PaintWheel.UI.NewPalette"
					: "Mods.PaintWheel.UI.NewEmptyPalette");

			case Part.Delete when Shown >= 0:
				return Language.GetTextValue(ArmedDelete == Shown ? "Mods.PaintWheel.UI.ConfirmDelete" : "Mods.PaintWheel.UI.DeletePalette");

			case Part.Rename when Shown >= 0:
				return Language.GetTextValue("Mods.PaintWheel.UI.RenamePalette");

			case Part.Pill: {
				int preset = PresetOf(HoveredIndex);
				return $"{PickerContent.PaletteName(preset, config)}   {PaletteEditor.CountOf(preset, config)}";
			}

			case Part.Slot when HoveredIndex >= 0 && HoveredIndex < PaletteEditor.GridPaints.Count: {
				int type = PaletteEditor.GridPaints[HoveredIndex];
				int total = PaintInventory.TotalStack(Main.LocalPlayer, type);
				string stack = total > 0 ? total.ToString() : Language.GetTextValue("Mods.PaintWheel.UI.Empty");
				int place = PaletteEditor.PlaceOf(type);

				// Its place in the ring, for a palette's own: the order the wheel deals them out in.
				return place > 0 && Shown >= 0
					? $"#{place}  {Lang.GetItemNameValue(type)}   {stack}"
					: $"{Lang.GetItemNameValue(type)}   {stack}";
			}
		}

		return Language.GetTextValue(Shown >= 0 ? "Mods.PaintWheel.UI.EditHint" : "Mods.PaintWheel.UI.AutoPaletteHint");
	}
}
