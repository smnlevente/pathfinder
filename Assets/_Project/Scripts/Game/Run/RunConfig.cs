using System;
using Pathfinder.Game.Content;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// Tuning for the run structure and the level types (GDD §10). Rewards that scale per galaxy are listed as
    /// [galaxy 1, galaxy 2, galaxy 3].
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ContentDefinition
    {
        [Serializable]
        public struct BossSetup
        {
            [Tooltip("Placeholder boss until the real bosses arrive in M5.")] public EnemyDefinition Enemy;
            public EliteModifierDefinition Elite;
            [Min(1f)] public float HealthMultiplier;
            public EnemyDefinition Escort;
            [Min(0)] public int EscortCount;
        }

        [field: Header("Arena")]
        [field: SerializeField] public Vector2 PlayerEntry { get; private set; } = new Vector2(0f, -14f);
        [field: SerializeField] public Vector2 ExitPosition { get; private set; } = new Vector2(0f, 14f);
        [field: SerializeField, Min(0.5f)] public float ExitRadius { get; private set; } = 1.6f;

        [field: Header("Scaling")]
        [field: SerializeField] public float[] GalaxyEnemyHealth { get; private set; } = { 1f, 1.6f, 2.4f };
        [field: SerializeField, Min(1f), Tooltip("Reward multiplier for levels revealed from a Mystery node.")] public float MysteryRewardMultiplier { get; private set; } = 1.5f;
        [field: SerializeField, Min(1f)] public float AmbushRewardMultiplier { get; private set; } = 2f;
        [field: SerializeField, Min(1f)] public float AmbushBudgetMultiplier { get; private set; } = 1.5f;

        [field: Header("Scraps level (GDD §10.1)")]
        [field: SerializeField, Min(1)] public int ScrapsWaves { get; private set; } = 4;
        [field: SerializeField, Min(0.1f)] public float ScrapsBudgetMultiplier { get; private set; } = 1.3f;
        [field: SerializeField, Min(1f)] public float ScrapsDropMultiplier { get; private set; } = 2f;
        [field: SerializeField] public int[] ScrapsCompletionBonus { get; private set; } = { 30, 45, 60 };

        [field: Header("Equipment level (GDD §10.2)")]
        [field: SerializeField, Min(1)] public int EquipmentWaves { get; private set; } = 3;
        [field: SerializeField] public EnemyDefinition CacheCarrier { get; private set; }
        [field: SerializeField] public EliteModifierDefinition CacheCarrierElite { get; private set; }
        [field: SerializeField, Min(0), Tooltip("Placeholder reward until equipment arrives in M4.")] public int CacheScraps { get; private set; } = 25;

        [field: Header("Dark Matter level (GDD §10.3)")]
        [field: SerializeField, Min(1)] public int DarkMatterNodesMin { get; private set; } = 8;
        [field: SerializeField, Min(1)] public int DarkMatterNodesMax { get; private set; } = 12;
        [field: SerializeField, Min(0.1f)] public float HarvestTime { get; private set; } = 1f;
        [field: SerializeField, Min(0.1f)] public float HarvestRadius { get; private set; } = 1.6f;
        [field: SerializeField] public float[] DarkMatterPerNode { get; private set; } = { 5f, 7.5f, 10f };
        [field: SerializeField, Min(1f)] public float InstabilityTime { get; private set; } = 90f;
        [field: SerializeField, Min(0.1f)] public float PressureBudgetMultiplier { get; private set; } = 0.5f;
        [field: SerializeField, Min(1f)] public float PressureWaveInterval { get; private set; } = 12f;
        [field: SerializeField, Min(0.1f)] public float EscalatedBudgetMultiplier { get; private set; } = 1.5f;
        [field: SerializeField, Min(1f)] public float EscalatedWaveInterval { get; private set; } = 5f;

        [field: Header("Repair level (GDD §10.4)")]
        [field: SerializeField, Range(0f, 1f)] public float RepairFraction { get; private set; } = 0.4f;
        [field: SerializeField, Min(0f)] public float ReinforceHull { get; private set; } = 15f;

        [field: Header("Shop level (GDD §10.6)")]
        [field: SerializeField, Min(0f), Tooltip("Scraps per point of hull.")] public float RepairPricePerHull { get; private set; } = 1f;

        [field: Header("Boss level (GDD §10.7)")]
        [field: SerializeField] public BossSetup Boss { get; private set; }
        [field: SerializeField] public int[] SingularityCores { get; private set; } = { 1, 2, 3 };
        [field: SerializeField, Range(0f, 1f)] public float BossHealFraction { get; private set; } = 0.3f;

        [field: Header("Objects")]
        [field: SerializeField] public GameObject ExitPrefab { get; private set; }
        [field: SerializeField] public GameObject DarkMatterNodePrefab { get; private set; }
        [field: SerializeField] public GameObject RepairStationPrefab { get; private set; }
        [field: SerializeField] public GameObject ShopTerminalPrefab { get; private set; }
        [field: SerializeField] public GameObject CachePrefab { get; private set; }

        public static T ForGalaxy<T>(T[] values, int galaxyIndex) =>
            values == null || values.Length == 0 ? default : values[Mathf.Clamp(galaxyIndex, 0, values.Length - 1)];
    }
}
