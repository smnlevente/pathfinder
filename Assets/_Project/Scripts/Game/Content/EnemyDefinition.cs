using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// Shared stats for an enemy archetype (GDD §7.3). Behaviour-specific tuning, such as a Gunner's burst,
    /// lives on the brain component in the prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Enemy", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ContentDefinition
    {
        [field: SerializeField, Min(1f)] public float MaxHealth { get; private set; } = 15f;
        [field: SerializeField, Min(0f)] public float MoveSpeed { get; private set; } = 5f;
        [field: SerializeField, Min(0f)] public float ContactDamage { get; private set; } = 10f;
        [field: SerializeField, Min(1), Tooltip("Wave director budget cost.")] public int BudgetCost { get; private set; } = 1;
        [field: SerializeField, Min(0), Tooltip("Scraps dropped on death (GDD §7.4). Elites multiply this.")] public int ScrapsDrop { get; private set; } = 1;
        [field: SerializeField, Range(0f, 1f)] public float ScrapsDropChance { get; private set; } = 0.5f;
        [field: SerializeField, Range(0f, 1f), Tooltip("How far knockback moves it: 1 = fully, 0 = immovable.")] public float KnockbackMultiplier { get; private set; } = 1f;
        [field: SerializeField] public Enemy Prefab { get; private set; }
        [field: SerializeField, Tooltip("An arena hazard rather than an enemy (an asteroid, a laser pylon): it never holds up a level's waves (M7).")]
        public bool IsHazard { get; private set; }
    }
}
