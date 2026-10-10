using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Localization;
using Pathfinder.Core.Run;
using Pathfinder.Game.Content;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.EditMode.Localization
{
    /// <summary>Every key the game asks for exists in en.loc, so no English player ever sees a [key] (GDD §12.5).</summary>
    public class LocalizationCoverageTests
    {
        static LocTable s_english;

        static LocTable English => s_english ??= LocTable.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Localization", "en.loc")));

        static void AssertKeys(IEnumerable<string> keys, string what)
        {
            var missing = keys.Where(key => !English.TryGet(key, out _)).Distinct().ToList();
            Assert.IsEmpty(missing, $"{what} missing from en.loc: {string.Join(", ", missing)}");
        }

        [Test]
        public void EnglishFile_ParsesCleanly()
        {
            var warnings = new List<string>();
            LocTable.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Localization", "en.loc")), warnings);
            Assert.IsEmpty(warnings, string.Join("\n", warnings));
            Assert.AreEqual("English", English.Meta["language"]);
        }

        [Test]
        public void EveryKeyInCode_IsInTheEnglishFile()
        {
            // Literal keys: Loc.T("key"...), plus key-like string literals in the localized UI code (option and menu tables).
            var literalKey = new Regex(@"""([a-z]+(?:\.[a-z0-9_\-]+)+)""");
            var sections = new HashSet<string>(English.Entries.Keys.Select(k => k.Split('.')[0]));
            var keys = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Scripts", "Game"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    int comment = line.IndexOf("//", StringComparison.Ordinal);
                    string code = comment >= 0 ? line.Substring(0, comment) : line;
                    foreach (Match match in literalKey.Matches(code))
                    {
                        string key = match.Groups[1].Value;
                        bool fileName = Regex.IsMatch(key, @"\.(json|jsonl|loc|tmp)$");
                        if (sections.Contains(key.Split('.')[0]) && !key.EndsWith(".tiered") && !fileName)
                            keys.Add(key);
                    }
                }
            }
            Assert.Greater(keys.Count, 100, "The scan should find the game's keys.");
            AssertKeys(keys, "Keys used in code");
        }

        [Test]
        public void BuiltKeys_AreInTheEnglishFile()
        {
            var keys = new List<string>();
            foreach (MapNodeType type in Enum.GetValues(typeof(MapNodeType)))
            {
                string name = type.ToString().ToLowerInvariant();
                keys.AddRange(new[] { $"level.{name}", $"level.{name}.desc", $"glyph.{name}" });
            }
            for (int galaxy = 0; galaxy < RunState.GalaxyCount; galaxy++)
                keys.Add($"galaxy.{galaxy}");
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                keys.Add($"rarity.{rarity.ToString().ToLowerInvariant()}");
            foreach (EquipmentCategory category in Enum.GetValues(typeof(EquipmentCategory)))
                keys.Add($"category.{category.ToString().ToLowerInvariant()}");
            foreach (StatId stat in Enum.GetValues(typeof(StatId)))
                keys.Add($"stat.{stat}");
            foreach (string control in new[] { "move", "aim", "fire", "secondary", "dash", "reload", "interact", "pause" })
                keys.AddRange(new[] { $"controls.{control}", $"controls.{control}.keys", $"controls.{control}.pad" });
            keys.AddRange(new[] { "ordinal.n", "meta.desc.stat.tiered", "meta.desc.scraps.tiered" });
            AssertKeys(keys, "Built keys");
        }

        [Test]
        public void EveryContentItem_HasItsText()
        {
            var keys = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ContentDefinition", new[] { "Assets/_Project/Data" }))
            {
                var content = AssetDatabase.LoadAssetAtPath<ContentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (content == null || content is WaveDirectorConfig || content is Pathfinder.Game.Run.RunConfig)
                    continue;
                keys.Add($"{content.Id}.name");

                // Stat items describe themselves from stat names; every other item has its own description template.
                if (content is EquipmentDefinition && !(content is StatEquipmentDefinition))
                    keys.Add($"{content.Id}.desc");
                if (content is ShipDefinition ship && !string.IsNullOrEmpty(ship.TraitName))
                    keys.AddRange(new[] { $"{content.Id}.trait", $"{content.Id}.trait.desc" });
                if (content is MysteryEventDefinition mystery)
                {
                    keys.Add($"{content.Id}.body");
                    for (int i = 0; i < mystery.Choices.Length; i++)
                    {
                        keys.Add($"{content.Id}.choice.{i}");
                        keys.Add($"{content.Id}.result.{i}");
                        if (mystery.Choices[i].Chance < 1f)
                            keys.Add($"{content.Id}.fail.{i}");
                    }
                }
            }
            Assert.Greater(keys.Count, 60);
            AssertKeys(keys, "Content text");
        }

        [Test]
        public void Lookup_FormatsAndFlagsMissingKeys()
        {
            Assert.AreEqual("40 SCRAPS", Loc.T("common.price", ("amount", 40)));
            LogAssert.Expect(LogType.Warning, new Regex("Missing key 'no.such.key'"));
            Assert.AreEqual("[no.such.key]", Loc.T("no.such.key"), "A missing key shows itself, so gaps are easy to spot.");
            Assert.AreEqual("asset text", Loc.TOr("no.such.content.name", "asset text"), "Content falls back to its asset text.");
        }
    }
}
