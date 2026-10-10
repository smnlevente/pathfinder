using System;
using System.Collections.Generic;
using System.IO;
using Pathfinder.Core.Localization;
using Pathfinder.Game.Settings;
using UnityEngine;

namespace Pathfinder.Game.Localization
{
    /// <summary>
    /// Every player-facing string (GDD §12.5). Text lives in StreamingAssets/Localization/{code}.loc, one
    /// <c>key = value</c> per line, so it can be edited and translated without touching code or rebuilding. A key missing
    /// from the current language falls back to English, then shows as <c>[key]</c> so gaps are easy to spot. Content
    /// (items, ships, events) uses keys made from its ID, falling back to the text authored on the asset.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";

        static LocTable s_english, s_current;
        static string s_language;
        static List<(string code, string name)> s_languages;
        static readonly HashSet<string> s_reported = new HashSet<string>();

        /// <summary>Raised after the language changes or the files are reloaded, so built screens can refresh.</summary>
        public static event Action Changed;

        public static string Folder => Path.Combine(Application.streamingAssetsPath, "Localization");

        public static string Language
        {
            get
            {
                EnsureLoaded();
                return s_language;
            }
        }

        /// <summary>Every .loc file in the folder: its code (the file name) and its display name (@language).</summary>
        public static IReadOnlyList<(string code, string name)> Languages
        {
            get
            {
                if (s_languages != null)
                    return s_languages;
                s_languages = new List<(string, string)>();
                if (Directory.Exists(Folder))
                {
                    foreach (string path in Directory.GetFiles(Folder, "*.loc"))
                    {
                        string code = Path.GetFileNameWithoutExtension(path);
                        var table = Read(path);
                        s_languages.Add((code, table != null && table.Meta.TryGetValue("language", out string name) ? name : code));
                    }
                }
                s_languages.Sort((a, b) => a.code == English ? -1 : b.code == English ? 1 : string.CompareOrdinal(a.name, b.name));
                return s_languages;
            }
        }

        public static void SetLanguage(string code)
        {
            GameSettings.Language = code;
            Reload();
        }

        /// <summary>Re-reads the .loc files, e.g. after editing them while the game runs (F9 in development builds).</summary>
        public static void Reload()
        {
            s_english = s_current = null;
            s_languages = null;
            s_reported.Clear();
            EnsureLoaded();
            Changed?.Invoke();
        }

        /// <summary>The text for a key.</summary>
        public static string T(string key) => Lookup(key) ?? Missing(key);

        /// <summary>The text for a key, with its placeholders filled: <c>T("common.price", ("amount", 40))</c>.</summary>
        public static string T(string key, params (string name, object value)[] args) => LocFormat.Format(T(key), args);

        /// <summary>The text for a key if any language has it, otherwise <paramref name="fallback"/>, e.g. a content asset's own text.</summary>
        public static string TOr(string key, string fallback) => Lookup(key) ?? fallback;

        public static string TOr(string key, string fallback, params (string name, object value)[] args) => LocFormat.Format(Lookup(key) ?? fallback, args);

        public static bool Has(string key) => Lookup(key) != null;

        static string Lookup(string key)
        {
            EnsureLoaded();
            if (s_current.TryGet(key, out string value))
                return value;
            return s_english.TryGet(key, out value) ? value : null;
        }

        static string Missing(string key)
        {
            if (s_reported.Add(key))
                Debug.LogWarning($"[Loc] Missing key '{key}' in {s_language}.loc and en.loc.");
            return $"[{key}]";
        }

        static void EnsureLoaded()
        {
            if (s_english != null)
                return;
            s_english = Read(Path.Combine(Folder, English + ".loc")) ?? new LocTable();
            s_language = GameSettings.Language;
            s_current = s_language == English ? s_english : Read(Path.Combine(Folder, s_language + ".loc")) ?? s_english;
            if (s_current == s_english)
                s_language = s_language == English || File.Exists(Path.Combine(Folder, s_language + ".loc")) ? s_language : English;
        }

        static LocTable Read(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var warnings = new List<string>();
                var table = LocTable.Parse(File.ReadAllText(path), warnings);
                foreach (string warning in warnings)
                    Debug.LogWarning($"[Loc] {Path.GetFileName(path)}: {warning}");
                return table;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Loc] Could not read {path}: {e.Message}");
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_english = s_current = null;
            s_languages = null;
            s_reported.Clear();
            Changed = null;
        }
    }
}
