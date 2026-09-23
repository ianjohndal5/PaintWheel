#!/usr/bin/env python3
"""Checks every translation against en-US before a release.

Run it from the mod root:

    python3 tools/checkloc.py

Separates what breaks from what is merely outstanding, because those need different
answers. tModLoader loads the fallback culture (English) before overlaying the active
one, so a key missing from a translation keeps its English value: the player sees an
English line, not a key name or a crash. That is a gap to fill, not a bug to block on.

  BREAKS (exit code 1):
    * placeholders ({0}, {1}, ...) differing from English - a dropped {0} loses the
      keybind name the line exists to show
    * a blank value where English has text - blank draws as nothing at all
    * a bare value hjson would misread - opening with { [ or ', starting with # or //, or
      just true/false/null - which stops the file loading or loses the entry; quote it.
      Checked in en-US too, since every language falls back to it

  OUTSTANDING (reported, exit code 0):
    * keys missing from a translation - shows English until translated
    * keys a translation has that English does not - dead weight, never read
    * values identical to English - usually untranslated, sometimes correct
      ("Grid", "Palette"), so judge them rather than trusting the count
    * lines longer than 110 characters - tooltips are drawn without wrapping, so a
      long line runs off the screen; break it the way the English file does

Filipino is an hjson like the rest, but Terraria has no Filipino culture, so tModLoader's
own loader skips it and Common/Systems/TranslationSystem.cs applies it at runtime instead.
It is checked here alongside the rest.

The game reads these files with the Hjson library. parse_hjson below covers only what
these files use: nested blocks, dotted names, quoted empties, triple-quoted blocks, and
the comments tModLoader's own localization updater writes.
"""

import json
import pathlib
import re
import sys

BASE = "en-US"
PREFIX = "Mods.PaintWheel."
LOCALIZATION = pathlib.Path("Localization")
MAX_LINE = 110
UNSAFE = "\x00unsafe:"


def unsafe_value(raw):
    """True for a bare value Hjson would not read as the text written."""
    if not raw:
        return False
    if raw.startswith('"'):
        return not (len(raw) >= 2 and raw.endswith('"'))  # text after the closing quote
    return (raw[0] in "{['" or raw.startswith(("#", "//", "/*"))
            or raw in ("true", "false", "null"))


def parse_hjson(text):
    """The slice of hjson these files use, comments included."""
    entries, path, lines, i = {}, [], text.lstrip("\ufeff").replace("\r\n", "\n").split("\n"), 0

    while i < len(lines):
        line = lines[i].strip()
        i += 1

        if not line or line.startswith(("#", "//")):
            continue

        # A block comment, which tModLoader uses to park a multi-line entry nobody has translated.
        if line.startswith("/*"):
            while "*/" not in line and i < len(lines):
                line = lines[i].strip()
                i += 1
            continue

        if line == "}":
            if path:
                path.pop()
            continue

        colon = line.find(":")
        if colon <= 0:
            continue

        name, value = line[:colon].strip(), line[colon + 1:].strip()

        if value == "{":
            path.append(name)
            continue

        # Values the game's Hjson parser reads as something other than plain text, which either stop
        # the whole file loading or quietly lose the entry: "{0} left" unquoted opens an object, a
        # leading # or // starts a comment, and so on. Flagged, not fixed.
        unsafe = unsafe_value(value)

        if not value and i < len(lines) and lines[i].strip() == "'''":
            block = []
            i += 1
            while i < len(lines) and lines[i].strip() != "'''":
                block.append(lines[i].strip())
                i += 1
            i += 1
            value = "\n".join(block)
        elif len(value) >= 2 and value[0] == '"' and value[-1] == '"':
            value = value[1:-1]

        if unsafe:
            value = UNSAFE + value

        entries[".".join(path + [name])] = value

    return entries


def load(path):
    if path.suffix == ".json":
        raw = json.loads(path.read_text(encoding="utf-8"))
        return {k[len(PREFIX):] if k.startswith(PREFIX) else k: v for k, v in raw.items()}

    return parse_hjson(path.read_text(encoding="utf-8"))


def placeholders(value):
    return sorted(set(re.findall(r"\{\d+\}", value)))


def report(label, keys, limit=4):
    shown = ", ".join(keys[:limit]) + (f" (+{len(keys) - limit} more)" if len(keys) > limit else "")
    return f"{label}: {shown}"


def main():
    base_path = LOCALIZATION / f"{BASE}_Mods.PaintWheel.hjson"
    if not base_path.exists():
        print(f"cannot find {base_path} - run this from the mod root", file=sys.stderr)
        return 2

    base = load(base_path)
    base_unsafe = sorted(k for k, v in base.items() if v.startswith(UNSAFE))
    base = {k: v.removeprefix(UNSAFE) for k, v in base.items()}
    others = sorted(p for p in LOCALIZATION.iterdir()
                    if p.name != base_path.name and p.suffix in {".hjson", ".json"})

    print(f"{BASE}: {len(base)} keys\n")
    failed = bool(base_unsafe)
    if base_unsafe:
        print("FAIL " + report(f"{base_path.name}: bare value hjson misreads - quote it", base_unsafe) + "\n")

    outstanding = 0

    for path in others:
        entries = load(path)

        blank = sorted(k for k, v in entries.items() if k in base and base[k].strip() and not v.strip())
        marks = sorted(k for k, v in entries.items()
                       if k in base and placeholders(v) != placeholders(base[k]))

        missing = sorted(set(base) - set(entries))
        extra = sorted(set(entries) - set(base))
        same = sorted(k for k, v in entries.items()
                      if k in base and v.strip() and v == base[k])

        unsafe = sorted(k for k, v in entries.items() if v.startswith(UNSAFE))
        entries = {k: v.removeprefix(UNSAFE) for k, v in entries.items()}

        breaks = [report("placeholders differ", marks)] if marks else []
        if unsafe:
            breaks.append(report("bare value hjson misreads (starts with { [ ' # // or is true/false/null) - quote it", unsafe))
        if blank:
            breaks.append(report("blank", blank))

        notes = []
        if missing:
            notes.append(report("missing, shows English", missing))
        if extra:
            notes.append(report("unknown", extra))
        if same:
            notes.append(f"same as English: {len(same)}")

        long_lines = sorted(k for k, v in entries.items() if any(len(part) > MAX_LINE for part in v.split("\n")))
        if long_lines:
            notes.append(report(f"lines over {MAX_LINE} characters, will run off screen", long_lines))

        failed |= bool(breaks)
        outstanding += len(missing) + len(extra)

        state = "FAIL" if breaks else "ok  "
        print(f"{state} {path.name:34} {len(entries)} keys")
        for line in breaks + notes:
            print(f"       {line}")

    if failed:
        print("\nsomething would draw wrong - fix before publishing")
    elif outstanding:
        print(f"\nnothing broken; {outstanding} keys outstanding, which show English until translated")
    else:
        print("\nevery file matches en-US")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
