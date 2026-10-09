using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>A magazine-fed primary weapon (GDD §6.1).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Primary Weapon", fileName = "Primary_")]
    public sealed class PrimaryWeaponDefinition : ContentDefinition
    {
        [field: Header("Firing")]
        [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 8f;
        [field: SerializeField, Min(0.1f)] public float ShotsPerSecond { get; private set; } = 10f;
        [field: SerializeField, Min(1)] public int MagazineSize { get; private set; } = 30;
        [field: SerializeField, Min(0.05f)] public float ReloadTime { get; private set; } = 1f;
        [field: SerializeField, Min(1)] public int ProjectilesPerShot { get; private set; } = 1;
        [field: SerializeField, Min(0f), Tooltip("Total cone angle in degrees.")] public float SpreadDegrees { get; private set; } = 2f;

        [field: Header("Projectile")]
        [field: SerializeField, Min(0.1f)] public float ProjectileSpeed { get; private set; } = 28f;
        [field: SerializeField, Min(0.1f)] public float Range { get; private set; } = 18f;
        [field: SerializeField, Min(0.01f)] public float ProjectileRadius { get; private set; } = 0.15f;
        [field: SerializeField] public GameObject ProjectilePrefab { get; private set; }
        [field: SerializeField] public GameObject ImpactPrefab { get; private set; }
    }
}
