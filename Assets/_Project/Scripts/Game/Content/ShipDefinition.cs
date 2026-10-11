using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>A pilotable ship's stat profile (GDD §5).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Ship", fileName = "Ship_")]
    public sealed class ShipDefinition : ContentDefinition
    {
        [field: Header("Trait")]
        [field: SerializeField] public string TraitName { get; private set; } = string.Empty;
        [field: SerializeField, TextArea] public string TraitDescription { get; private set; } = string.Empty;

        /// <summary>The localized trait name (<c>{id}.trait</c>), or the one authored on the asset.</summary>
        public string TraitTitle => Localization.Loc.TOr($"{Id}.trait", TraitName);

        /// <summary>The localized trait description (<c>{id}.trait.desc</c>), or the one authored on the asset.</summary>
        public string TraitText => Localization.Loc.TOr($"{Id}.trait.desc", TraitDescription);

        [field: SerializeField, Tooltip("Optional always-on effect, e.g. the Stormbringer's Lightning Core. Stat-only traits live in the stats below.")]
        public Pathfinder.Game.Equipment.EquipmentDefinition Trait { get; private set; }

        [field: SerializeField, Min(0), Tooltip("Extra options in every equipment choice and Shop catalog (Arcanum Explorer: 1).")]
        public int ExtraEquipmentChoices { get; private set; }

        [field: Header("Look")]
        [field: SerializeField, Tooltip("The hull's colour, so each ship looks like itself (M7).")] public Color HullTint { get; private set; } = Color.white;

        [field: Header("Durability")]
        [field: SerializeField, Min(1f)] public float MaxHull { get; private set; } = 100f;
        [field: SerializeField, Min(0f)] public float MaxShield { get; private set; } = 25f;
        [field: SerializeField, Min(0f), Tooltip("Shield points per second while regenerating.")] public float ShieldRegenRate { get; private set; } = 4f;
        [field: SerializeField, Min(0f), Tooltip("Seconds without damage before the shield regenerates.")] public float ShieldRegenDelay { get; private set; } = 5f;
        [field: SerializeField, Min(0f), Tooltip("Invulnerability after a hull hit.")] public float HitInvulnerability { get; private set; } = 0.3f;
        [field: SerializeField, Min(0f)] public float DamageTakenMultiplier { get; private set; } = 1f;

        [field: Header("Movement")]
        [field: SerializeField, Min(0.1f)] public float MoveSpeed { get; private set; } = 8f;
        [field: SerializeField, Min(0.01f), Tooltip("Seconds to reach full speed from standstill.")] public float AccelerationTime { get; private set; } = 0.08f;
        [field: SerializeField, Min(0f), Tooltip("Radius within which pickups fly to the ship.")] public float PickupMagnetRadius { get; private set; } = 3f;

        [field: Header("Dash")]
        [field: SerializeField, Min(0.1f)] public float DashDistance { get; private set; } = 5f;
        [field: SerializeField, Min(0.01f)] public float DashDuration { get; private set; } = 0.15f;
        [field: SerializeField, Min(1)] public int DashCharges { get; private set; } = 1;
        [field: SerializeField, Min(0.05f)] public float DashRechargeTime { get; private set; } = 1.2f;
    }
}
