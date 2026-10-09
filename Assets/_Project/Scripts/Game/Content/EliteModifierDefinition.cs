using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>A modifier that turns a normal enemy into an elite (GDD §7.3).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Elite Modifier", fileName = "Elite_")]
    public sealed class EliteModifierDefinition : ContentDefinition
    {
        [field: SerializeField, Min(0.1f)] public float HealthMultiplier { get; private set; } = 1f;
        [field: SerializeField, Min(0.1f)] public float MoveSpeedMultiplier { get; private set; } = 1f;
        [field: SerializeField, Min(0.1f), Tooltip("Speeds up attack timers: cooldowns, wind-ups, bursts.")] public float AttackRateMultiplier { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float ScrapsMultiplier { get; private set; } = 3f;
        [field: SerializeField, Min(0.5f), Tooltip("Body and hitbox scale.")] public float ScaleMultiplier { get; private set; } = 1.2f;
        [field: SerializeField, Tooltip("Visual marker attached to the elite.")] public GameObject MarkerPrefab { get; private set; }
    }
}
