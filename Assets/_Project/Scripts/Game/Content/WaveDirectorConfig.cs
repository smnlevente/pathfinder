using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// Wave director tuning for a galaxy theme (GDD §7.2): which enemies can appear, how the budget grows, and
    /// how often elites appear.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Wave Director Config", fileName = "Waves_")]
    public sealed class WaveDirectorConfig : ContentDefinition
    {
        [Serializable]
        public struct Entry
        {
            public EnemyDefinition Enemy;
            [Min(0f)] public float Weight;
            [Min(1), Tooltip("First wave in which this enemy may appear.")] public int MinWave;
        }

        [field: Header("Enemy pool")]
        [field: SerializeField] public Entry[] Enemies { get; private set; } = new Entry[0];
        [field: SerializeField] public EliteModifierDefinition[] Elites { get; private set; } = new EliteModifierDefinition[0];

        [field: Header("Budget")]
        [field: SerializeField, Min(1)] public int BaseBudget { get; private set; } = 6;
        [field: SerializeField, Min(0f), Tooltip("Budget increase per wave as a fraction of the base (endless mode).")] public float BudgetGrowthPerWave { get; private set; } = 0.25f;
        [field: SerializeField, Tooltip("Budget multiplier for galaxy 1, 2, 3.")] public float[] GalaxyBudgetMultipliers { get; private set; } = { 1f, 1.4f, 1.8f };
        [field: SerializeField, Min(0f), Tooltip("Extra budget per map row beyond the first, as a fraction.")] public float BudgetPerRow { get; private set; } = 0.15f;

        [field: Header("Elites")]
        [field: SerializeField, Range(0f, 1f)] public float EliteChanceBase { get; private set; } = 0.03f;
        [field: SerializeField, Range(0f, 1f)] public float EliteChancePerWave { get; private set; } = 0.02f;
        [field: SerializeField, Range(0f, 1f)] public float EliteChancePerRow { get; private set; } = 0.04f;
        [field: SerializeField, Range(0f, 1f)] public float EliteChancePerGalaxy { get; private set; } = 0.05f;
        [field: SerializeField, Range(0f, 1f)] public float EliteChanceMax { get; private set; } = 0.35f;
        [field: SerializeField, Min(1f)] public float EliteCostMultiplier { get; private set; } = 2f;

        [field: Header("Pacing")]
        [field: SerializeField, Min(0f)] public float FirstWaveDelay { get; private set; } = 1.5f;
        [field: SerializeField, Range(0.1f, 1f), Tooltip("Fraction of a wave that must die before the next starts.")] public float ClearFraction { get; private set; } = 0.8f;
        [field: SerializeField, Min(1f)] public float WaveTimeout { get; private set; } = 25f;
        [field: SerializeField, Min(0f)] public float TelegraphDuration { get; private set; } = 1f;
        [field: SerializeField, Min(0f), Tooltip("Delay between successive spawns within a wave.")] public float SpawnStagger { get; private set; } = 0.08f;

        [field: Header("Placement")]
        [field: SerializeField, Min(0f)] public float MinPlayerDistance { get; private set; } = 6f;
        [field: SerializeField, Min(0f)] public float ArenaInset { get; private set; } = 3f;

        public int Budget(int wave, int galaxyIndex, int row)
        {
            float galaxy = GalaxyBudgetMultipliers.Length > 0 ? GalaxyBudgetMultipliers[Mathf.Clamp(galaxyIndex, 0, GalaxyBudgetMultipliers.Length - 1)] : 1f;
            float rowFactor = 1f + BudgetPerRow * Mathf.Max(0, row - 1);
            float waveFactor = 1f + BudgetGrowthPerWave * Mathf.Max(0, wave - 1);
            return Mathf.RoundToInt(BaseBudget * galaxy * rowFactor * waveFactor);
        }

        public float EliteChance(int wave, int galaxyIndex, int row) => Mathf.Min(EliteChanceMax,
            EliteChanceBase + EliteChancePerWave * Mathf.Max(0, wave - 1) + EliteChancePerRow * Mathf.Max(0, row - 1) + EliteChancePerGalaxy * galaxyIndex);

        /// <summary>Finds an enemy in the pool by content ID, or null.</summary>
        public EnemyDefinition FindEnemy(string id)
        {
            foreach (var entry in Enemies)
            {
                if (entry.Enemy != null && entry.Enemy.Id == id)
                    return entry.Enemy;
            }
            return null;
        }

        public EliteModifierDefinition FindElite(string id)
        {
            foreach (var elite in Elites)
            {
                if (elite != null && elite.Id == id)
                    return elite;
            }
            return null;
        }

        public IEnumerable<EnemyDefinition> AllEnemies()
        {
            foreach (var entry in Enemies)
                yield return entry.Enemy;
        }
    }
}
