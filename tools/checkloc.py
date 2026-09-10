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

  OUTSTANDING (reported, exit code 0):
    * keys missing from a translation - shows English until translated
    * keys a translation has that English does not - dead weight, never read
    * values identical to English - usually untranslated, sometimes correct
      ("Grid", "Palette"), so judge them rather than trusting the count

Filipino lives in Localization/fil-PH.json instead of an hjson, because Terraria has no
Filipino culture. It is applied at runtime by Common/Translations.cs and is checked here
alongside the rest.
"""

import json
import pathlib
import re
import sys

BASE = "en-US"
PREFIX = "Mods.PaintWheel."
LOCALIZATION = pathlib.Path("Localization")


def parse_hjson(text):
    """The slice of hjson these files use. Mirrors ParseHjson in Common/Translations.cs."""
    entries, path, lines, i = {}, [], text.replace("\r\n", "\n").split("\n"), 0

    while i < len(lines):
        line = lines[i].strip()
        i += 1

        if not line or line.startswith("#"):
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
    others = sorted(p for p in LOCALIZATION.iterdir()
                    if p.name != base_path.name and p.suffix in {".hjson", ".json"})

    print(f"{BASE}: {len(base)} keys\n")
    failed = False

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

        breaks = [report("placeholders differ", marks)] if marks else []
        if blank:
            breaks.append(report("blank", blank))

        notes = []
        if missing:
            notes.append(report("missing, shows English", missing))
        if extra:
            notes.append(report("unknown", extra))
        if same:
            notes.append(f"same as English: {len(same)}")

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
