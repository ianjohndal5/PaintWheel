using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Hjson;
using Newtonsoft.Json.Linq;
using PaintWheel.Common.Configs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PaintWheel.Common.Systems;

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
public class TranslationSystem : ModSystem
{
	// Every key this may write. Enforced rather than assumed: the loader would happily assign any
	// existing key, and quietly rewriting another mod's text is exactly the kind of thing that makes
	// somebody else's mod look broken.
	private const string OwnKeys = "Mods.PaintWheel.";

	private const string Fallback = "en-US";
	// Named like the others, which tModLoader's own loader skips: it keeps only files whose name holds
	// a culture it knows, and fil-PH is not one (LocalizationLoader.TryGetCultureAndPrefixFromPath).
	private const string FilipinoFile = "Localization/fil-PH_Mods.PaintWheel.hjson";

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

	/// <summary>
	/// The language last put in place, so a config save that changed something else - the palette
	/// editor saves on every click - does not rewrite every entry again.
	/// </summary>
	private static LanguageOverride? applied;

	public override void Load()
	{
		if (!Main.dedServ)
			mod = Mod;
	}

	public override void Unload()
	{
		mod = null;
		applied = null;
		cache.Clear();
	}

	/// <summary>
	/// Runs whenever the game loads its text - at startup, on every language change, and on tModLoader's
	/// live reload - which overwrites our entries. What it just loaded is exactly Automatic, with any
	/// translation mod already layered on top, so only a language chosen in the config goes back over it.
	/// </summary>
	public override void OnLocalizationsLoaded()
	{
		applied = LanguageOverride.Automatic;
		Apply();
	}

	/// <summary>Puts the chosen language's text in place, unless it already is. Safe to call whenever.</summary>
	public static void Apply()
	{
		if (Main.dedServ || mod is null)
			return;

		LanguageOverride chosen = PaintWheelConfig.Instance?.Language ?? LanguageOverride.Automatic;
		if (applied == chosen)
			return;

		// English first, so a line a translation does not have yet reads in English rather than in
		// whichever language was chosen before - the same fallback the game gives its own text.
		string english = CultureFile(Fallback);
		Push(Read(english));

		// Automatic writes too: whatever was chosen before is still sitting in those entries, and the
		// game only reloads them when the language itself changes.
		string path = chosen switch {
			LanguageOverride.Automatic => ActiveFile(),
			LanguageOverride.Filipino => FilipinoFile,
			// A value this list does not know - a hand-edited config - falls back to the game's language.
			_ => Cultures.TryGetValue(chosen, out string culture) ? CultureFile(culture) : ActiveFile(),
		};

		if (path != english)
			Push(Read(path));

		applied = chosen;
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
				using var reader = new StreamReader(new MemoryStream(mod.GetFileBytes(path)), Encoding.UTF8,
					detectEncodingFromByteOrderMarks: true);

				text = Flatten(reader.ReadToEnd());
			}
		}
		catch (Exception exception) {
			mod.Logger.Error($"Could not read {path}, so that language will do nothing.", exception);
		}

		cache[path] = text;
		return text;
	}

	/// <summary>
	/// Flattens an hjson file the way tModLoader's own <c>LocalizationLoader</c> does, with the same
	/// parser, so comments, quoting and multi-line blocks come out exactly as the game would show them.
	/// Names are joined by walking the parents rather than read off <c>JToken.Path</c>, which brackets a
	/// name holding a dot - and <c>OpenWheel.DisplayName</c> is one name. The files carry no culture
	/// prefix of their own, so ours is added here.
	/// </summary>
	private static Dictionary<string, string> Flatten(string hjson)
	{
		var text = new Dictionary<string, string>();

		foreach (JToken token in JObject.Parse(HjsonValue.Parse(hjson).ToString()).SelectTokens("$..*")) {
			if (token.HasValues || token is JObject { Count: 0 })
				continue;

			string key = "";
			JToken child = token;

			for (JToken parent = token.Parent; parent is not null; parent = parent.Parent) {
				string rest = key.Length == 0 ? "" : "." + key;

				if (parent is JProperty property)
					key = property.Name + rest;
				else if (parent is JArray array)
					key = array.IndexOf(child) + rest;

				child = parent;
			}

			text[OwnKeys + key.Replace(".$parentVal", "")] = token.ToString();
		}

		return text;
	}

	/// <summary>The loader takes {"Category": {"Key": "text"}}, so a flat key splits at its last dot.</summary>
	private static void Push(Dictionary<string, string> text)
	{
		if (text is null)
			return;

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
