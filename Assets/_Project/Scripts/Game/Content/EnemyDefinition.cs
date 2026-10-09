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
        [field: SerializeField] public Enemy Prefab { get; private set; }
    }
}
