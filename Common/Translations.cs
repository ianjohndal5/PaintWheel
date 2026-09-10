using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using PaintWheel.Configs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PaintWheel.Common;

/// <summary>
/// Lets the mod's own text use a language of your choosing, whatever the game is set to - including
/// Filipino, which Terraria cannot be set to at all: <c>GameCulture</c> knows nine cultures and that is
/// not one of them, so a <c>fil-PH</c> file would sit in the mod and never be read.
/// <para/>
/// Every language is read back out of this mod's own packed files and written over its own entries, so
/// returning to Automatic restores exactly what the game would have loaded rather than something
/// remembered earlier. <c>LoadLanguageFromFileTextJson</c> with <c>canCreateCategories: false</c> only
/// assigns keys that already exist, and the prefix check below means those can only ever be ours.
/// </summary>
public static class Translations
{
	// Every key this may write. Enforced rather than assumed: the loader would happily assign any
	// existing key, and quietly rewriting another mod's text is exactly the kind of thing that makes
	// somebody else's mod look broken.
	private const string OwnKeys = "Mods.PaintWheel.";

	private const string Fallback = "en-US";
	private const string FilipinoFile = "Localization/fil-PH.json";

	private static readonly Dictionary<LanguageOverride, string> Cultures = new() {
		[LanguageOverride.English] = "en-US",
		[LanguageOverride.German] = "de-DE",
		[LanguageOverride.Spanish] = "es-ES",
		[LanguageOverride.French] = "fr-FR",
		[LanguageOverride.Italian] = "it-IT",
		[LanguageOverride.Polish] = "pl-PL",
		[LanguageOverride.Portuguese] = "pt-BR",
		[LanguageOverride.Russian] = "ru-RU",
		[LanguageOverride.Chinese] = "zh-Hans",
	};

	private static readonly Dictionary<string, Dictionary<string, string>> cache = new();

	private static Mod mod;
	private static bool hooked;

	public static void Load(Mod owner)
	{
		if (Main.dedServ)
			return;

		mod = owner;
		LanguageManager.Instance.OnLanguageChanged += LanguageChanged;
		hooked = true;
	}

	public static void Unload()
	{
		if (hooked)
			LanguageManager.Instance.OnLanguageChanged -= LanguageChanged;

		hooked = false;
		mod = null;
		cache.Clear();
	}

	// Changing the game's language reloads every entry from file, so a chosen override has to go back.
	private static void LanguageChanged(LanguageManager manager) => Apply();

	/// <summary>Puts the chosen language's text in place. Safe to call whenever.</summary>
	public static void Apply()
	{
		if (Main.dedServ || mod is null)
			return;

		LanguageOverride chosen = PaintWheelConfig.Instance?.Language ?? LanguageOverride.Automatic;

		// Automatic writes too: whatever was chosen before is still sitting in those entries, and the
		// game only reloads them when the language itself changes.
		string path = chosen switch {
			LanguageOverride.Automatic => ActiveFile(),
			LanguageOverride.Filipino => FilipinoFile,
			_ => CultureFile(Cultures[chosen]),
		};

		Dictionary<string, string> text = Read(path);
		if (text is not null)
			Push(text);
	}

	private static string ActiveFile()
	{
		string path = CultureFile(Language.ActiveCulture?.Name ?? Fallback);

		return mod.FileExists(path) ? path : CultureFile(Fallback);
	}

	private static string CultureFile(string culture) => $"Localization/{culture}_Mods.PaintWheel.hjson";

	private static Dictionary<string, string> Read(string path)
	{
		if (cache.TryGetValue(path, out Dictionary<string, string> cached))
			return cached;

		Dictionary<string, string> text = null;

		try {
			if (mod.FileExists(path)) {
				string content = Encoding.UTF8.GetString(mod.GetFileBytes(path));

				text = path.EndsWith(".json", StringComparison.Ordinal)
					? JsonSerializer.Deserialize<Dictionary<string, string>>(content)
					: ParseHjson(content);
			}
		}
		catch (Exception exception) {
			mod.Logger.Error($"Could not read {path}, so that language will do nothing.", exception);
		}

		cache[path] = text;
		return text;
	}

	/// <summary>
	/// Reads the slice of hjson this mod's own files use: nested blocks, dotted names, quoted empties
	/// and triple quoted blocks. It is only ever pointed at files packed into this mod, which is what
	/// lets it stay this small.
	/// </summary>
	private static Dictionary<string, string> ParseHjson(string content)
	{
		var text = new Dictionary<string, string>();
		var path = new List<string>();
		string[] lines = content.Replace("\r\n", "\n").Split('\n');

		for (int i = 0; i < lines.Length; i++) {
			string line = lines[i].Trim();
			if (line.Length == 0 || line.StartsWith('#'))
				continue;

			if (line == "}") {
				if (path.Count > 0)
					path.RemoveAt(path.Count - 1);

				continue;
			}

			int colon = line.IndexOf(':');
			if (colon <= 0)
				continue;

			string name = line[..colon].Trim();
			string value = line[(colon + 1)..].Trim();

			if (value == "{") {
				path.Add(name);
				continue;
			}

			// A block opens on the line after its name and runs to the closing quotes. The leading tabs
			// are the file's own indentation rather than part of the text.
			if (value.Length == 0 && i + 1 < lines.Length && lines[i + 1].Trim() == "'''") {
				var block = new List<string>();
				i += 2;

				while (i < lines.Length && lines[i].Trim() != "'''") {
					block.Add(lines[i].Trim());
					i++;
				}

				value = string.Join('\n', block);
			}
			else if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
				value = value[1..^1];
			}

			string prefix = path.Count > 0 ? string.Join('.', path) + "." : "";
			text[OwnKeys + prefix + name] = value;
		}

		return text;
	}

	/// <summary>The loader takes {"Category": {"Key": "text"}}, so a flat key splits at its last dot.</summary>
	private static void Push(Dictionary<string, string> text)
	{
		var grouped = new Dictionary<string, Dictionary<string, string>>();
		LanguageManager language = LanguageManager.Instance;

		foreach ((string key, string value) in text) {
			int split = key.LastIndexOf('.');
			if (split <= 0 || !key.StartsWith(OwnKeys, StringComparison.Ordinal) || !language.Exists(key))
				continue;

			string category = key[..split];
			if (!grouped.TryGetValue(category, out Dictionary<string, string> entries))
				grouped[category] = entries = new Dictionary<string, string>();

			entries[key[(split + 1)..]] = value;
		}

		if (grouped.Count > 0)
			language.LoadLanguageFromFileTextJson(JsonSerializer.Serialize(grouped), canCreateCategories: false);
	}
}
