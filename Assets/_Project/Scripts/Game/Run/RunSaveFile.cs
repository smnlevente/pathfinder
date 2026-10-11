using System;
using System.Collections.Generic;
using System.IO;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Content;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// The run in progress, saved as readable JSON next to the profile so it can be continued after quitting (M7, GDD §14.2).
    /// It's written each time the galaxy map comes up, so it always holds a whole number of levels: quitting mid-level picks
    /// up from the map before that level. It's deleted when the run ends. No encryption: it's a single-player game, and a
    /// readable file is easy to debug. A file with an unknown version, or one that doesn't parse, is ignored. Version 2
    /// (M8) keeps every local co-op pilot; a version 1 file, with its one pilot, still loads.
    /// </summary>
    public static class RunSaveFile
    {
        public const int Version = 2;

        [Serializable]
        public sealed class Data
        {
            public int version = Version;
            public string seed;
            public string ship, primary, secondary;
            public int galaxy;
            public int currentNode = -1;
            public List<int> visited = new List<int>();
            public int levelsCleared;
            public int darkMatter, singularityCores;
            public List<string> seenEvents = new List<string>();
            public bool repairUsed;
            public float minutes;

            /// <summary>Version 1's single pilot. Version 2 writes the first pilot here too, and every pilot in <see cref="pilots"/>.</summary>
            public Pilot pilot = new Pilot();

            /// <summary>Every local pilot, player 1 first (M8).</summary>
            public List<Pilot> pilots = new List<Pilot>();
        }

        [Serializable]
        public sealed class Pilot
        {
            public string ship, primary, secondary;
            public float hull;
            public int scraps, scrapsCollected;
            public List<Item> equipment = new List<Item>();
            public List<Modifier> runModifiers = new List<Modifier>();
        }

        [Serializable]
        public sealed class Item
        {
            public string id;
            public int level;
        }

        [Serializable]
        public sealed class Modifier
        {
            public string stat;
            public float flat, percent;
        }

        /// <summary>
        /// The save file: run.json beside meta.json. It follows the meta save's name, so a test that points the meta save at
        /// its own temporary file gets its own run file too.
        /// </summary>
        public static string FilePath
        {
            get
            {
                string meta = MetaProgression.SavePath;
                string name = Path.GetFileName(meta);
                string runName = name.Contains("meta") ? name.Replace("meta", "run") : "run-" + name;
                return Path.Combine(Path.GetDirectoryName(meta) ?? Application.persistentDataPath, runName);
            }
        }

        public static bool Exists => TryLoad(out _);

        /// <summary>Saves the run with one pilot.</summary>
        public static void Save(RunState run, PlayerShip pilot, float minutes) =>
            Save(run, pilot != null ? new[] { pilot } : null, minutes);

        /// <summary>Saves the run with its local pilots, player 1 first. Downed co-op pilots are saved too.</summary>
        public static void Save(RunState run, IReadOnlyList<PlayerShip> pilots, float minutes)
        {
            if (run == null || pilots == null || pilots.Count == 0 || pilots[0] == null || run.IsOver)
                return;

            var snapshot = run.Snapshot();
            var data = new Data
            {
                seed = RunSeed.ToCode(snapshot.Seed),
                ship = snapshot.Loadout.ShipId,
                primary = snapshot.Loadout.PrimaryId,
                secondary = snapshot.Loadout.SecondaryId,
                galaxy = snapshot.GalaxyIndex,
                currentNode = snapshot.CurrentNodeId,
                visited = new List<int>(snapshot.Visited),
                levelsCleared = snapshot.LevelsCleared,
                darkMatter = snapshot.DarkMatter,
                singularityCores = snapshot.SingularityCores,
                seenEvents = new List<string>(snapshot.SeenEvents),
                repairUsed = snapshot.RepairUsed,
                minutes = minutes,
            };
            foreach (var pilot in pilots)
            {
                if (pilot != null)
                    data.pilots.Add(SavePilot(pilot));
            }
            data.pilot = data.pilots[0];

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] Could not write {FilePath}: {e.Message}");
            }
        }

        static Pilot SavePilot(PlayerShip pilot)
        {
            var saved = new Pilot
            {
                ship = pilot.Definition.Id,
                primary = pilot.PrimaryWeapon != null ? pilot.PrimaryWeapon.Id : string.Empty,
                secondary = pilot.SecondaryWeapon != null ? pilot.SecondaryWeapon.Id : string.Empty,
                hull = pilot.Vitals.Hull, // 0 for a downed co-op pilot: they come back as if revived
                scraps = pilot.Scraps,
                scrapsCollected = pilot.TotalScrapsGained,
            };
            foreach (var entry in pilot.Equipment.All)
                saved.equipment.Add(new Item { id = entry.Item.Id, level = entry.Level });
            foreach (var modifier in pilot.RunModifiers)
                saved.runModifiers.Add(new Modifier { stat = modifier.Stat.ToString(), flat = modifier.Flat, percent = modifier.Percent });
            return saved;
        }

        public static bool TryLoad(out Data data)
        {
            data = null;
            try
            {
                if (!File.Exists(FilePath))
                    return false;
                data = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] Ignoring {FilePath}: {e.Message}");
                data = null;
            }
            if (data == null || data.version < 1 || data.version > Version || !RunSeed.TryParse(data.seed, out _))
            {
                data = null;
                return false;
            }
            // Version 1 had a single pilot.
            data.pilots ??= new List<Pilot>();
            if (data.pilots.Count == 0 && data.pilot != null)
                data.pilots.Add(data.pilot);
            return true;
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] Could not delete {FilePath}: {e.Message}");
            }
        }

        /// <summary>
        /// Gives up a saved run for a new one. What it earned is banked as a lost run, as if it had been abandoned, so starting
        /// over never throws progress away.
        /// </summary>
        public static void DiscardAndBank()
        {
            if (!TryLoad(out var data))
            {
                Delete();
                return;
            }
            // Co-op pilots share one wallet (M8), so the first pilot's count is everyone's.
            int collected = data.pilots.Count > 0 ? data.pilots[0].scrapsCollected : data.pilot.scrapsCollected;
            MetaProgression.Current.BankRun(data.darkMatter, data.singularityCores, collected, won: false, data.galaxy + 1);
            MetaProgression.Save();
            Delete();
        }

        /// <summary>The run as it stood when saved.</summary>
        public static RunState ToRunState(Data data)
        {
            RunSeed.TryParse(data.seed, out var seed);
            return RunState.Restore(new RunStateSnapshot
            {
                Seed = seed,
                Loadout = new PilotLoadout(data.ship, data.primary, data.secondary),
                GalaxyIndex = data.galaxy,
                CurrentNodeId = data.currentNode,
                Visited = data.visited.ToArray(),
                LevelsCleared = data.levelsCleared,
                DarkMatter = data.darkMatter,
                SingularityCores = data.singularityCores,
                SeenEvents = data.seenEvents.ToArray(),
                RepairUsed = data.repairUsed,
            });
        }

        /// <summary>
        /// Puts a freshly spawned pilot back as saved: weapons, equipment and levels, run modifiers (repairs, events, the
        /// Lab's bonuses as they were when the run began), Scraps and hull. Content that no longer exists is skipped.
        /// </summary>
        public static void ApplyPilot(Pilot saved, PlayerShip pilot, ContentDatabase content)
        {
            pilot.ClearRunState();
            var primary = content.FindPrimary(saved.primary);
            if (primary != null && primary != pilot.PrimaryWeapon)
                pilot.EquipPrimary(primary);
            var secondary = content.FindSecondary(saved.secondary);
            if (secondary != null && secondary != pilot.SecondaryWeapon)
                pilot.EquipSecondary(secondary);

            foreach (var modifier in saved.runModifiers)
            {
                if (Enum.TryParse(modifier.stat, out StatId stat))
                    pilot.AddRunModifier(stat, modifier.flat, modifier.percent);
            }
            foreach (var item in saved.equipment)
            {
                var definition = content.FindEquipment(item.id);
                if (definition == null || pilot.Equip(definition) != EquipOutcome.Equipped)
                    continue;
                for (int level = 1; level < item.level; level++)
                    pilot.UpgradeEquipment(definition);
            }
            pilot.RestoreScraps(saved.scraps, saved.scrapsCollected);
            // A pilot saved while downed comes back as if revived.
            pilot.RestoreHull(saved.hull > 0f ? saved.hull : pilot.Vitals.MaxHull * CoopRules.ReviveHullFraction);
        }
    }
}
