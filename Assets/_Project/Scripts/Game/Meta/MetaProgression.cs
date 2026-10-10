using System;
using System.IO;
using Pathfinder.Core.Meta;
using Pathfinder.Game.Content;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Meta
{
    /// <summary>
    /// Loads, saves and applies the persistent progression (GDD §11, §14.2), stored as JSON in
    /// <c>Application.persistentDataPath</c>.
    /// </summary>
    public static class MetaProgression
    {
        const string FileName = "meta.json";

        static MetaState s_current;
        static string s_pathOverride;

        public static MetaState Current => s_current ??= Load();

        /// <summary>The save file location. Tests point it at a temporary file so they never touch a real save.</summary>
        public static string SavePath
        {
            get => s_pathOverride ?? Path.Combine(Application.persistentDataPath, FileName);
            set
            {
                s_pathOverride = value;
                s_current = null;
            }
        }

        public static MetaState Load()
        {
            try
            {
                if (File.Exists(SavePath))
                    return MetaState.FromSave(JsonUtility.FromJson<MetaSave>(File.ReadAllText(SavePath)));
            }
            catch (Exception e)
            {
                // A corrupt save must never block the game. Start fresh and keep the bad file for inspection.
                Debug.LogWarning($"[Meta] Could not read {SavePath}: {e.Message}. Starting with a fresh profile.");
                try { File.Copy(SavePath, SavePath + ".corrupt", overwrite: true); } catch (IOException) { }
            }
            return new MetaState();
        }

        public static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // Write to a temporary file first so a crash mid-write can't destroy the existing save.
                string temporary = SavePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(Current.ToSave(), prettyPrint: true));
                if (File.Exists(SavePath))
                    File.Replace(temporary, SavePath, null);
                else
                    File.Move(temporary, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Meta] Could not save progression to {SavePath}: {e.Message}");
            }
        }

        /// <summary>Forgets the loaded state, so the next access reloads from disk.</summary>
        public static void Reload() => s_current = null;

        /// <summary>
        /// Whether content can be picked in the loadout. Content is available unless an unlock upgrade gates it and that
        /// unlock hasn't been bought.
        /// </summary>
        public static bool IsAvailable(ContentDefinition content, ContentDatabase database)
        {
            foreach (var upgrade in database.MetaUpgrades)
            {
                if (upgrade.Effect == MetaUpgradeDefinition.EffectKind.Unlock && upgrade.UnlockTarget == content)
                    return Current.IsUnlocked(upgrade.Id);
            }
            return true;
        }

        public static bool TryPurchase(MetaUpgradeDefinition upgrade)
        {
            bool bought = upgrade.Effect == MetaUpgradeDefinition.EffectKind.Unlock
                ? Current.TryUnlock(upgrade.Id, upgrade.Currency, upgrade.CostOfTier(0))
                : Current.TryPurchaseTier(upgrade.Id, upgrade.MaxTiers, upgrade.Currency, upgrade.CostOfTier(Current.Tier(upgrade.Id)));
            if (bought)
                Save();
            return bought;
        }

        /// <summary>Applies owned upgrades to a pilot at the start of a run.</summary>
        public static void ApplyRunBonuses(PlayerShip pilot, ContentDatabase database)
        {
            foreach (var upgrade in database.MetaUpgrades)
            {
                int tier = Current.Tier(upgrade.Id);
                if (tier <= 0)
                    continue;

                switch (upgrade.Effect)
                {
                    case MetaUpgradeDefinition.EffectKind.StatBonus:
                        pilot.AddRunModifier(upgrade.Stat, upgrade.AmountPerTier * tier, 0f);
                        break;
                    case MetaUpgradeDefinition.EffectKind.StartingScraps:
                        pilot.AddStartingScraps(Mathf.RoundToInt(upgrade.AmountPerTier * tier));
                        break;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_current = null;
            s_pathOverride = null;
        }
    }
}
