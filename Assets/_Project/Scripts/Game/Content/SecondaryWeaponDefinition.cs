using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// A cooldown-based burst ability (GDD §6.2). Each subclass implements its own effect. Activation runs
    /// on the authority, inside the simulation tick.
    /// </summary>
    public abstract class SecondaryWeaponDefinition : ContentDefinition
    {
        [field: SerializeField, Min(0.1f)] public float Cooldown { get; private set; } = 8f;
        [field: SerializeField, Min(1)] public int Charges { get; private set; } = 1;

        [field: SerializeField, Tooltip("Presentation-only effect spawned at the ship on activation.")]
        public GameObject ActivationEffect { get; private set; }

        public abstract void Activate(PlayerShip ship, CombatWorld world, IRandom random);
    }
}
