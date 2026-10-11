using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>
    /// The Bot-Commander Frigate's trait (GDD §5.3, M7): every kill launches a guided bot from the wreck. It homes on the
    /// nearest enemy and bursts on contact. A few at a time; a kill that would launch one more is skipped.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Swarm Protocol (trait)", fileName = "Trait_SwarmProtocol")]
    public sealed class SwarmProtocolDefinition : EquipmentDefinition
    {
        [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 12f;
        [field: SerializeField, Min(1)] public int MaxBots { get; private set; } = 6;
        [field: SerializeField, Min(0.1f)] public float Speed { get; private set; } = 9f;
        [field: SerializeField, Min(0f), Tooltip("Degrees per second a bot can turn toward its target.")] public float TurnRate { get; private set; } = 420f;
        [field: SerializeField, Min(0.05f)] public float Radius { get; private set; } = 0.3f;
        [field: SerializeField, Min(1f)] public float Range { get; private set; } = 30f;
        [field: SerializeField] public GameObject BotPrefab { get; private set; }
        [field: SerializeField] public GameObject ImpactPrefab { get; private set; }

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) => Localization.Loc.T($"{Id}.desc", ("damage", Damage), ("max", MaxBots));

        sealed class Effect : EquipmentEffect
        {
            readonly SwarmProtocolDefinition _definition;

            public Effect(SwarmProtocolDefinition definition) => _definition = definition;

            public override void OnKill(Enemy enemy, in DamageInfo info)
            {
                var world = Ship.World;
                if (world == null || !Authority.IsAuthority || world.Projectiles.CountFrom(Ship.EntityId, _definition.BotPrefab) >= _definition.MaxBots)
                    return;

                // Off it goes in a random direction, then turns toward the nearest enemy.
                Vector2 direction = PlaneMath.FromHeading(Random.Range(0f, 360f));
                world.Projectiles.Spawn(new ProjectileSpawn(enemy.Position, direction, _definition.Speed, _definition.Damage, _definition.Radius,
                    _definition.Range, Ship.Team, Ship.EntityId, _definition.BotPrefab, _definition.ImpactPrefab,
                    turnRate: _definition.TurnRate, kind: DamageKind.Proc));
            }
        }
    }
}
