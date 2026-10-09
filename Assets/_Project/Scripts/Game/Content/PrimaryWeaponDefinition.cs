using UnityEngine;

namespace Pathfinder.Game.Content
{
    public enum PrimaryFireMode
    {
        /// <summary>Fires while held, with a magazine and reload (Photon Repeater, Plasma Cutter).</summary>
        Automatic,

        /// <summary>Hold to charge, release to fire. No magazine, but it can overheat (Quantum Charger).</summary>
        Charge,
    }

    /// <summary>A primary weapon (GDD §6.1).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Primary Weapon", fileName = "Primary_")]
    public sealed class PrimaryWeaponDefinition : ContentDefinition
    {
        [field: SerializeField] public PrimaryFireMode FireMode { get; private set; } = PrimaryFireMode.Automatic;

        [field: Header("Damage")]
        [field: SerializeField, Min(0f), Tooltip("Per projectile. For charge weapons, the damage at minimum charge.")] public float Damage { get; private set; } = 8f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Damage multiplier at maximum range (1 = no falloff).")] public float DamageAtMaxRange { get; private set; } = 1f;
        [field: SerializeField, Min(0), Tooltip("Extra enemies each projectile passes through.")] public int Pierce { get; private set; }

        [field: Header("Automatic")]
        [field: SerializeField, Min(0.1f)] public float ShotsPerSecond { get; private set; } = 10f;
        [field: SerializeField, Min(1)] public int MagazineSize { get; private set; } = 30;
        [field: SerializeField, Min(0.05f)] public float ReloadTime { get; private set; } = 1f;
        [field: SerializeField, Min(1)] public int ProjectilesPerShot { get; private set; } = 1;
        [field: SerializeField, Min(0f), Tooltip("Total cone angle in degrees. Multi-projectile shots spread evenly across it.")] public float SpreadDegrees { get; private set; } = 2f;

        [field: Header("Charge")]
        [field: SerializeField, Min(0f)] public float MinChargeTime { get; private set; } = 0.3f;
        [field: SerializeField, Min(0.05f)] public float FullChargeTime { get; private set; } = 1.5f;
        [field: SerializeField, Min(0f)] public float MaxChargeDamage { get; private set; } = 120f;
        [field: SerializeField, Min(0f), Tooltip("Seconds a full charge can be held before overheating.")] public float OverheatTime { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float LockoutTime { get; private set; } = 1f;
        [field: SerializeField, Min(0.01f)] public float MaxChargeRadius { get; private set; } = 0.5f;
        [field: SerializeField, Min(1f), Tooltip("Visual scale of a fully charged shot.")] public float MaxChargeVisualScale { get; private set; } = 2.2f;

        [field: Header("Projectile")]
        [field: SerializeField, Min(0.1f)] public float ProjectileSpeed { get; private set; } = 28f;
        [field: SerializeField, Min(0.1f)] public float Range { get; private set; } = 18f;
        [field: SerializeField, Min(0.01f)] public float ProjectileRadius { get; private set; } = 0.15f;
        [field: SerializeField] public GameObject ProjectilePrefab { get; private set; }
        [field: SerializeField] public GameObject ImpactPrefab { get; private set; }
    }
}
