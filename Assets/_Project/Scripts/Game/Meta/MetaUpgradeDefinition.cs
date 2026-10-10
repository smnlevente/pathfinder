using Pathfinder.Core.Equipment;
using Pathfinder.Core.Meta;
using Pathfinder.Game.Content;
using UnityEngine;

namespace Pathfinder.Game.Meta
{
    /// <summary>
    /// A permanent Hub upgrade or unlock (GDD §11.2): Lab Upgrades bought with Dark Matter and Core Projects bought with
    /// Singularity Cores. Each tier has its own cost.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Meta Upgrade", fileName = "Meta_")]
    public sealed class MetaUpgradeDefinition : ContentDefinition
    {
        public enum EffectKind
        {
            /// <summary>Adds a flat stat bonus per tier at the start of every run.</summary>
            StatBonus,

            /// <summary>Starts every run with extra Scraps per tier.</summary>
            StartingScraps,

            /// <summary>Makes a piece of content available, e.g. a weapon in the loadout (single tier).</summary>
            Unlock,
        }

        [field: SerializeField] public MetaCurrency Currency { get; private set; } = MetaCurrency.DarkMatter;
        [field: SerializeField, Tooltip("Cost of each tier. The array length is the number of tiers.")] public int[] Costs { get; private set; } = { 30 };
        [field: SerializeField] public EffectKind Effect { get; private set; } = EffectKind.StatBonus;
        [field: SerializeField] public StatId Stat { get; private set; } = StatId.MaxHull;
        [field: SerializeField] public float AmountPerTier { get; private set; } = 10f;
        [field: SerializeField, Tooltip("For unlocks: the content made available.")] public ContentDefinition UnlockTarget { get; private set; }

        public int MaxTiers => Effect == EffectKind.Unlock ? 1 : Costs.Length;

        public int CostOfTier(int tier) => Costs.Length == 0 ? 0 : Costs[Mathf.Clamp(tier, 0, Costs.Length - 1)];

        public string Describe()
        {
            string perTier = MaxTiers > 1 ? " per tier" : string.Empty;
            return Effect switch
            {
                EffectKind.StatBonus => $"+{AmountPerTier:0.#} {Equipment.StatNames.Name(Stat)}{perTier}",
                EffectKind.StartingScraps => $"Start each run with +{AmountPerTier:0} Scraps{perTier}",
                _ => UnlockTarget != null ? $"Unlocks the {UnlockTarget.DisplayName}" : "Unlock",
            };
        }
    }
}
