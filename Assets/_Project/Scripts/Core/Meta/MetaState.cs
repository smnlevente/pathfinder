using System;
using System.Collections.Generic;

namespace Pathfinder.Core.Meta
{
    public enum MetaCurrency
    {
        DarkMatter,
        SingularityCores,
    }

    /// <summary>What a finished run paid into the meta progression (GDD §11.1).</summary>
    public readonly struct RunRewards
    {
        public int DarkMatter { get; }

        /// <summary>Dark Matter converted from 10% of the run's Scraps.</summary>
        public int ConvertedScraps { get; }

        public int SingularityCores { get; }

        public int TotalDarkMatter => DarkMatter + ConvertedScraps;

        public RunRewards(int darkMatter, int convertedScraps, int singularityCores)
        {
            DarkMatter = darkMatter;
            ConvertedScraps = convertedScraps;
            SingularityCores = singularityCores;
        }
    }

    /// <summary>
    /// Persistent progression between runs (GDD §11): banked currencies, Hub upgrade tiers, unlocks and run statistics.
    /// Pure logic. The game layer persists it through <see cref="MetaSave"/>.
    /// </summary>
    public sealed class MetaState
    {
        /// <summary>Fraction of a run's Scraps converted to Dark Matter when it ends (GDD §11.1).</summary>
        public const float ScrapsConversion = 0.1f;

        readonly Dictionary<string, int> _tiers = new Dictionary<string, int>();
        readonly HashSet<string> _unlocks = new HashSet<string>();

        public int DarkMatter { get; private set; }
        public int SingularityCores { get; private set; }
        public int RunsPlayed { get; private set; }
        public int RunsWon { get; private set; }

        /// <summary>Highest galaxy reached (1-based), for the Hub's statistics.</summary>
        public int BestGalaxy { get; private set; }

        public int Balance(MetaCurrency currency) => currency == MetaCurrency.DarkMatter ? DarkMatter : SingularityCores;

        public int Tier(string upgradeId) => _tiers.TryGetValue(upgradeId, out int tier) ? tier : 0;

        public bool IsUnlocked(string id) => _unlocks.Contains(id);

        /// <summary>Buys the next tier of an upgrade if it isn't maxed and the player can afford it.</summary>
        public bool TryPurchaseTier(string upgradeId, int maxTiers, MetaCurrency currency, int cost)
        {
            int tier = Tier(upgradeId);
            if (tier >= maxTiers || !TrySpend(currency, cost))
                return false;

            _tiers[upgradeId] = tier + 1;
            return true;
        }

        /// <summary>Unlocks content, e.g. a weapon, if it isn't already unlocked and the player can afford it.</summary>
        public bool TryUnlock(string id, MetaCurrency currency, int cost)
        {
            if (IsUnlocked(id) || !TrySpend(currency, cost))
                return false;

            _unlocks.Add(id);
            return true;
        }

        /// <summary>Banks a finished run, won or lost. Persistent currencies are always kept (GDD §3.1).</summary>
        public RunRewards BankRun(int darkMatter, int singularityCores, int scrapsCollected, bool won, int galaxyReached)
        {
            var rewards = new RunRewards(Math.Max(0, darkMatter), (int)(Math.Max(0, scrapsCollected) * ScrapsConversion), Math.Max(0, singularityCores));
            DarkMatter += rewards.TotalDarkMatter;
            SingularityCores += rewards.SingularityCores;
            RunsPlayed++;
            if (won)
                RunsWon++;
            BestGalaxy = Math.Max(BestGalaxy, galaxyReached);
            return rewards;
        }

        bool TrySpend(MetaCurrency currency, int cost)
        {
            if (cost < 0 || Balance(currency) < cost)
                return false;

            if (currency == MetaCurrency.DarkMatter)
                DarkMatter -= cost;
            else
                SingularityCores -= cost;
            return true;
        }

        public MetaSave ToSave()
        {
            var save = new MetaSave
            {
                DarkMatter = DarkMatter,
                SingularityCores = SingularityCores,
                RunsPlayed = RunsPlayed,
                RunsWon = RunsWon,
                BestGalaxy = BestGalaxy,
            };
            save.Unlocks.AddRange(_unlocks);
            save.Unlocks.Sort(StringComparer.Ordinal);
            foreach (var pair in _tiers)
            {
                save.UpgradeIds.Add(pair.Key);
                save.UpgradeTiers.Add(pair.Value);
            }
            return save;
        }

        public static MetaState FromSave(MetaSave save)
        {
            var state = new MetaState();
            if (save == null)
                return state;

            state.DarkMatter = Math.Max(0, save.DarkMatter);
            state.SingularityCores = Math.Max(0, save.SingularityCores);
            state.RunsPlayed = save.RunsPlayed;
            state.RunsWon = save.RunsWon;
            state.BestGalaxy = save.BestGalaxy;
            foreach (string id in save.Unlocks)
                state._unlocks.Add(id);
            for (int i = 0; i < Math.Min(save.UpgradeIds.Count, save.UpgradeTiers.Count); i++)
                state._tiers[save.UpgradeIds[i]] = save.UpgradeTiers[i];
            return state;
        }
    }

    /// <summary>
    /// The persisted form of <see cref="MetaState"/>. Content is referenced by ID (GDD §14.4). Plain fields keep it
    /// serializable by any JSON serializer, including Unity's.
    /// </summary>
    [Serializable]
    public sealed class MetaSave
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int DarkMatter;
        public int SingularityCores;
        public int RunsPlayed;
        public int RunsWon;
        public int BestGalaxy;
        public List<string> Unlocks = new List<string>();
        public List<string> UpgradeIds = new List<string>();
        public List<int> UpgradeTiers = new List<int>();
    }
}
