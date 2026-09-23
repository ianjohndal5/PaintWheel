# Paint Wheel

A client-side [tModLoader](https://github.com/tModLoader/tModLoader) mod for Terraria that puts a
colour picker on your paint tools.

Hold a Paintbrush, Paint Roller or Paint Scraper and press right click. A wheel of colours opens at
your cursor; flick toward one, release, and every paint stroke from then on uses that paint. No
inventory shuffling, no dragging stacks into the first slot.

Fully client side — no server install, no netcode, and by default nothing about painting touches your
inventory.

- **Author:** SugarDark
- **Version:** 1.0
- **Side:** Client

---

## Contents

- [Installing](#installing)
- [Quick start](#quick-start)
- [What it does](#what-it-does)
- [Keybinds](#keybinds)
- [Palettes](#palettes)
- [Scrape mode](#scrape-mode)
- [Settings](#settings)
- [Languages](#languages)
- [Compatibility](#compatibility)
- [Troubleshooting](#troubleshooting)
- [Building from source](#building-from-source)
- [Project layout](#project-layout)
- [License](#license)

---

## Installing

Subscribe on the tModLoader Steam Workshop, or drop the built `.tmod` into your
`Terraria/tModLoader/Mods` folder and enable it from the in-game mod list.

Nothing else is required. Because the mod is client side, you can join any server with it on whether
or not anyone else is running it.

## Quick start

1. Hold a Paintbrush, Paint Roller or Paint Scraper.
2. Press and hold **right click**. The wheel opens at your cursor, showing every paint you carry.
3. Flick toward the colour you want and let go.
4. Paint as normal. That colour is now what every stroke uses.

Releasing near the centre cancels and keeps the paint you already had.

Prefer not to hold the button? Set **Right click behaviour → Click to open** in the config: one press
leaves the picker up, and a colour closes it.

## What it does

- **Three picker shapes,** switchable in the config: a radial **Wheel** of colour discs, a vertical
  **Bar** of colour bands you sweep down, or a **Grid** that lays every swatch out in rows so nothing
  needs paging.
- **Swatches are the paint's real colour,** not an item sprite in a box. Item sprites are an option if
  you want them.
- **A supply gauge** appears on a swatch once that stack starts running down — the swatch fades and a
  ring of pips around it drains.
- **A paint you have run out of keeps its place,** dimmed and struck through, so a saved palette never
  comes up blank and the layout never shifts under your hand mid-build. The auto-filled palette does
  the same for anything you have carried since entering the world.
- **Paging.** Arrows and the scroll wheel step through a palette holding more than one ring's worth, so
  no colour is ever hidden.
- **Number keys pick directly.** While the picker is up, `1`–`0` choose the first ten swatches (or rows
  of a list) instead of switching the hotbar, and a small digit on each shows which is which. Stack
  counts on the swatches are an option too.
- **Gamepad.** On the wheel the right stick points at a swatch, the way a radial menu is used; in the
  bar, the grid and the lists it moves across the whole picker.
- **Paint both halves with one tool.** A switch on the bottom row (or `Z`) lets the brush and the roller
  each paint blocks *and* walls: the tool's own half first, then the other one once that is done, so
  holding the mouse on a tile lands both. `Q` trades the brush in hand for the roller and back.
- **A coating row** (none / Illuminant / Echo) sits underneath. With a paint *and* a coating picked,
  painting applies whichever the target tile is still missing, so holding the mouse on a tile lands
  both.
- **Paint Sprayer support.** Blocks you place take the colour you picked. No Sprayer? There is a
  setting that grants the same thing.
- **Placing blocks bare.** A struck-through brush in the same row means "no paint", so blocks go down
  unpainted without unequipping anything. Click it again to go back to the colour you were using. A
  coating you picked still applies.
- **An eyedropper** that copies the paint off any tile or wall — or a paint or coating off your
  inventory or a chest, by hovering it and pressing the key, even while you are carrying a stack on the
  cursor. A dot of that colour sits beside the cursor while you hover, gold once it is the one you have
  selected.
- **A cursor readout** where the game shows nothing to go by: what a placed block will get from the
  Sprayer (the coating, when you have picked one, since a placed block is painted once), the
  paint-both switch on a brush or roller, and which half a restricted scraper takes.
- **It tells you when nothing happens.** "Out of Red Paint" floats up the moment your chosen paint or
  coating runs dry, choosing a paint you do not carry says so, and so does a picker that cannot open
  (inventory open, or no paint to offer) — rather than leaving the button to seem dead.

## Keybinds

All of these are rebindable in **Settings → Controls**. Several let you work without opening the
picker at all. With *Only while holding a paint tool* on (the default) they answer only while you hold
one — or a block, with the Paint Sprayer's effect on — except the eyedropper on an inventory slot, and
leaving scrape mode, which always work.

| Action | Default | What it does |
| --- | --- | --- |
| Open Paint Wheel | `V` | Opens the picker. Hold, flick and release like right click, or tap it to leave the picker open. |
| Paint Eyedropper | Middle mouse | Copies the paint off the tile or wall under the cursor, or the paint or coating off an inventory slot. |
| Swap To Previous Paint | `X` | Flips between your two most recent paints. |
| Previous Palette | `[` | Steps back through your palettes. |
| Next Palette | `]` | Steps forward through your palettes. |
| Toggle Scrape Mode | `G` | Enters or leaves scrape mode. |
| Toggle No Paint | `N` | Flips between placing blocks bare and the colour you were using. |
| Previous Paint | `,` | Steps back one colour in the current palette. |
| Next Paint | `.` | Steps forward one colour in the current palette. |
| Swap Brush And Roller | `Q` | Trades the Paintbrush in hand for the Paint Roller, or back. Same swap as scrape mode's. |
| Toggle Paint Blocks And Walls | `Z` | Lets either tool paint both halves of a tile, or goes back to one half each. |

Whatever you switch to floats its name above your character, in its own colour.

While the picker is open, your **hotbar keys** (`1`–`0` unless you have rebound them) pick its first
ten swatches or list rows instead of switching the hotbar.

## Palettes

Click the **centre of the wheel**, or the **palette name** above the picker in any shape, to open your
palette list.

- There is always one auto-filled palette — **"Everything you own"** — holding every paint you are
  carrying, plus anything you have carried since entering the world (struck through once used up, so
  the ring does not close up under your hand).
- Below it sits one entry per palette you have saved. A long list scrolls.
- Click the one you want. The choice is remembered per character.
- Each saved palette has an edit, a rename and a delete button. Delete takes a second click to
  confirm, since a deleted palette is gone for good. Rename edits the name in place: Enter keeps it,
  Escape cancels.

Saved palettes are built from a single palette block in the config: every paint the game has, laid out
for you to click and add or remove. No item pickers full of swords. You can also build one in-game
with the **+** in the list — from what you are carrying, or empty if you carry none — then edit it:
its colours stay in the order you added them, which is the order they sit in the ring.

## Scrape mode

Click the scraper disc on the bottom row and the wheel becomes a scraper menu:

- **Blocks and walls**
- **Blocks only**
- **Walls only** — something the vanilla scraper cannot do at all.
- **Coatings only** — strips Illuminant or Echo and leaves the paint under it. The vanilla scraper
  always takes both together.

It needs a Paint Scraper, and it takes one into your hand for you: if the scraper is already on your
hotbar it just switches to that slot, and if it is in your bag it swaps into the slot you were
holding. Leaving the mode puts both back — unless you have moved either since, in which case your
inventory is left as you arranged it. The swap-back is remembered for the session only: scrape mode
itself stays on across a save and reload, but after one, leaving it just switches mode. Apart from
this, the brush/roller swap key and the *Inventory swap* fallback in Advanced, nothing in the mod
moves an item.

## Settings

Open **Settings → Mod Configuration → Paint Wheel**. Only the settings people actually change sit on
the front page; the rest live behind their own buttons.

### Front page

| Setting | Default | Notes |
| --- | --- | --- |
| Language | Automatic | Follows the game's language, or forces one. See [Languages](#languages). |
| Open with right click | On | Turn off if another mod wants right click on paint tools. The keybind still works. |
| Right click behaviour | Hold to open | Or *Click to open*, which leaves the picker up until you are done. |
| Only while holding a paint tool | On | Restricts the wheel and its keybinds to paint tools (and to holding blocks with a Paint Sprayer equipped). |
| Play sounds | On | Menu ticks while flicking, and a grab sound on commit. |
| Picker shape | Wheel | Wheel, Bar or Grid. |
| Palettes | — | Your saved palettes. |

### Sizes and cosmetics

Show item sprites and a background panel (both off by default), number keys on the swatches (on),
stack counts (off) and the cursor readout (on), plus the geometry: wheel radius,
swatch size, centre dead zone, swatches per ring (capped at 12 — more than that stops being
flickable), open animation length, and the bar layout's band width, height and bands per page.

### Supply readout

How a swatch shows what is left of that paint: the stack size at which fading starts (default 60,
counted across every slot holding that paint), how faint a nearly empty swatch may get, and the low
supply ring and its pip count. **Hide empty swatches** is off by default, so an empty swatch keeps its
position rather than shifting the layout under your hand.

### Advanced

| Setting | Default | Notes |
| --- | --- | --- |
| Paint override method | Detour | How your choice is forced onto vanilla's painting code. See [Compatibility](#compatibility). |
| Use any paint when the choice runs out | Off | Off means painting stops rather than silently spending a different colour. |
| Show coating row | On | The Illuminant / Echo row under the wheel. |
| Smart coating order | On | Applies whichever of paint or coating the target is still missing. Off means the coating always wins. |
| Paint blocks as you place them | Off | Grants the Paint Sprayer's effect without the accessory. The Sprayer icon in the builder toggles turns it off again. |

## Languages

The mod ships translations for **German, Spanish, French, Italian, Polish, Portuguese, Russian and
Chinese**, and follows your game language automatically.

**Filipino** is also included, but is picked in the mod config rather than followed automatically,
because Terraria has no Filipino language setting to follow. The Language setting can also make the
mod speak one language while the game speaks another.

## Compatibility

- **Modded paint scrapers are picked up automatically** — anything registered as a scraper works with
  the wheel and with scrape mode.
- **Modded brushes and rollers are not,** and cannot be. Vanilla's painting code tests for the four
  hardcoded brush and roller ids, so nothing else paints through it however it is registered.
- **Right click conflicts.** If another mod wants right click on paint tools, turn off *Open with right
  click* and use the keybind instead.
- **How painting is applied.** By default the mod detours `Player.FindPaintOrCoating`, the single
  vanilla lookup that both picks the paint and consumes it. Because selection and consumption share
  one reference, the colour applied and the stack spent can never disagree, and your inventory is
  never rearranged.

  If another mod fights that detour, **Advanced → Paint override method → Inventory swap** is the
  fallback: it swaps your chosen stack into the slot vanilla would pick for a fraction of one tick and
  swaps it straight back.

## Troubleshooting

**The wheel opens but nothing gets painted.** Check that the paint you picked has not run out — an
empty swatch stays in place, dimmed and struck through, and "Out of …" floats up when it happens. By
default painting stops rather than spending a different colour; *Advanced → Use any paint when the
choice runs out* changes that.

**Blocks I place are not painted.** That is the Paint Sprayer's job. Equip one, or turn on
*Advanced → Paint blocks as you place them* — and check the Sprayer's builder toggle is on.

**Blocks I place get the coating but not the colour.** The game paints a placed block once, so with
both a paint and a coating chosen it gets only one, and *Smart coating order* picks the coating a new
block is always missing. Brush over it afterwards for the colour.

**"Paint Wheel hit an error and has stopped applying your paint."** The mod caught an exception and
disabled its own hook rather than break your game. Details are in `client.log`; switching *Paint
override method* to Inventory swap is worth trying if another mod is involved.

**Scrape mode says there is no scraper to hold.** It needs a Paint Scraper somewhere in your inventory
or hotbar. The brush/roller swap is the same: it needs the other tool somewhere on you.

**"Close the inventory to open Paint Wheel."** The picker does not open over the inventory, where a
right click belongs to the slots. Close it first, or use the eyedropper key on a paint in a slot.

## Building from source

Clone into your `Terraria/tModLoader/ModSources` folder so that the folder is exactly
`ModSources/PaintWheel` (`git clone <url> PaintWheel`), then build from the in-game **Mod Sources**
menu or open `PaintWheel.csproj` in your IDE. The project imports `../tModLoader.targets`, which
tModLoader keeps in `ModSources`, so a checkout nested any deeper will not build.

Note that `build.txt`, `description.txt` and `description_workshop.txt` are not tracked in this
repository. tModLoader needs `build.txt` to build the mod, so create one before building:

```
displayName = Paint Wheel
author = SugarDark
version = 1.0
side = Client

buildIgnore = README.md, tools/*, Properties/*
```

`tools/checkloc.py` checks the localization files for drift between languages, and for the mistakes
that stop a file loading at all. Run it from the mod root before a release. It, this README and
`Properties/` are excluded from the shipped `.tmod` via `buildIgnore` — they are developer-facing
only.

## License

Paint Wheel is licensed under the [Mozilla Public License 2.0](LICENSE).

You are free to use, modify and redistribute it, including as part of a larger
work released under different terms. If you modify a file covered by this
license and distribute the result, that file's source must remain available
under the MPL.

Copyright (c) 2026 Ian John L. Dal (SugarDark)
