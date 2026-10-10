using Pathfinder.Game.Content;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// Tuning for Survival (GDD §3.2): how often upgrades and guardians come, and how the pressure grows with the clock.
    /// Values listed per tier are [before the first guardian, after it, after the second].
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Survival Config", fileName = "SurvivalConfig")]
    public sealed class SurvivalConfig : ContentDefinition
    {
        [field: Header("Clock")]
        [field: SerializeField, Min(5f), Tooltip("Seconds on the clock between equipment choices.")] public float UpgradeInterval { get; private set; } = 60f;
        [field: SerializeField, Min(10f), Tooltip("Seconds on the clock between guardians. The clock holds while one is alive.")] public float GuardianInterval { get; private set; } = 300f;
        [field: SerializeField, Min(0f), Tooltip("Seconds of warning before a guardian arrives.")] public float GuardianWarning { get; private set; } = 3f;
        [field: SerializeField, Min(1)] public int UpgradeChoices { get; private set; } = 3;
        [field: SerializeField, Range(0f, 1f), Tooltip("Hull restored for skipping an upgrade, as a fraction of max hull.")] public float SkipRepairFraction { get; private set; } = 0.2f;

        [field: Header("Pressure")]
        [field: SerializeField, Min(1)] public int BaseBudget { get; private set; } = 6;
        [field: SerializeField, Min(0f), Tooltip("Wave budget growth per minute on the clock, as a fraction of the base.")] public float BudgetPerMinute { get; private set; } = 0.35f;
        [field: SerializeField, Min(1f), Tooltip("Seconds before the next wave comes anyway.")] public float WaveInterval { get; private set; } = 15f;
        [field: SerializeField, Min(1), Tooltip("No timed wave starts while this many enemies are alive.")] public int MaxEnemies { get; private set; } = 45;
        [field: SerializeField, Range(0f, 1f), Tooltip("Wave budget while a guardian is in the arena.")] public float GuardianBudgetMultiplier { get; private set; } = 0.35f;
        [field: SerializeField, Min(1f)] public float GuardianWaveInterval { get; private set; } = 18f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Each enemy's chance to be an elite at the start.")] public float EliteChanceBase { get; private set; } = 0.03f;
        [field: SerializeField, Range(0f, 1f)] public float EliteChancePerMinute { get; private set; } = 0.015f;
        [field: SerializeField, Range(0f, 1f)] public float EliteChanceMax { get; private set; } = 0.3f;
        [field: SerializeField, Tooltip("Enemy health multiplier at each guardian interval on the clock (0, 5, 10, 15 minutes); it keeps growing along the last step after that.")]
        public float[] EnemyHealth { get; private set; } = { 1f, 1.6f, 2.4f, 3.2f };

        [field: Header("Rewards")]
        [field: SerializeField, Tooltip("Dark Matter for each minute on the clock, per tier.")] public int[] DarkMatterPerMinute { get; private set; } = { 4, 6, 8 };
        [field: SerializeField, Min(0f), Tooltip("Seconds the Scraps on the floor fly home after a guardian falls.")] public float VacuumSeconds { get; private set; } = 3f;

        /// <summary>The wave budget at a point on the clock.</summary>
        public int Budget(float clockSeconds) => Mathf.Max(1, Mathf.RoundToInt(BaseBudget * (1f + BudgetPerMinute * clockSeconds / 60f)));

        public float EliteChance(float clockSeconds) => Mathf.Min(EliteChanceMax, EliteChanceBase + EliteChancePerMinute * clockSeconds / 60f);

        /// <summary>The enemy health multiplier at a point on the clock: a straight line between the listed values.</summary>
        public float EnemyHealthAt(float clockSeconds)
        {
            if (EnemyHealth == null || EnemyHealth.Length == 0)
                return 1f;
            if (EnemyHealth.Length == 1)
                return EnemyHealth[0];

            float steps = Mathf.Max(0f, clockSeconds) / GuardianInterval;
            int index = Mathf.Min(Mathf.FloorToInt(steps), EnemyHealth.Length - 2);
            return Mathf.LerpUnclamped(EnemyHealth[index], EnemyHealth[index + 1], steps - index);
        }
    }
}
