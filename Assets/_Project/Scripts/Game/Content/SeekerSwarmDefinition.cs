using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>Seeker Swarm: a fan of homing missiles, each assigned to a different nearby enemy.</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Secondary/Seeker Swarm", fileName = "Secondary_SeekerSwarm")]
    public sealed class SeekerSwarmDefinition : SecondaryWeaponDefinition
    {
        [field: SerializeField, Min(1)] public int MissileCount { get; private set; } = 6;
        [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 20f;
        [field: SerializeField, Min(0.1f)] public float Speed { get; private set; } = 14f;
        [field: SerializeField, Min(0f), Tooltip("Degrees per second a missile can turn toward its target.")] public float TurnRate { get; private set; } = 300f;
        [field: SerializeField, Min(0.1f)] public float Range { get; private set; } = 30f;
        [field: SerializeField, Min(0.01f)] public float Radius { get; private set; } = 0.2f;
        [field: SerializeField, Range(0f, 360f), Tooltip("Launch fan around the aim direction.")] public float LaunchArc { get; private set; } = 140f;
        [field: SerializeField] public GameObject ProjectilePrefab { get; private set; }
        [field: SerializeField] public GameObject ImpactPrefab { get; private set; }

        static readonly List<CombatEntity> s_targets = new List<CombatEntity>();

        public override void Activate(PlayerShip ship, CombatWorld world, IRandom random)
        {
            // Spread missiles over the nearest enemies instead of overkilling one. Armour is skipped, so boss weak
            // points are found.
            s_targets.Clear();
            foreach (var enemy in CombatRegistry.OnTeam(ship.Team.Opponent()))
            {
                if (enemy.IsAlive && !enemy.IsIntangible && !enemy.Armoured)
                    s_targets.Add(enemy);
            }
            s_targets.Sort((a, b) => (a.Position - ship.Position).sqrMagnitude.CompareTo((b.Position - ship.Position).sqrMagnitude));

            for (int i = 0; i < MissileCount; i++)
            {
                float t = MissileCount > 1 ? i / (MissileCount - 1f) - 0.5f : 0f;
                Vector2 direction = PlaneMath.Rotate(ship.AimDirection, t * LaunchArc);
                int targetId = s_targets.Count > 0 ? s_targets[i % s_targets.Count].EntityId : 0;

                world.Projectiles.Spawn(new ProjectileSpawn(
                    ship.Position + direction * 0.6f, direction, Speed, Damage * ship.SecondaryDamageMultiplier, Radius, Range, ship.Team, ship.EntityId,
                    ProjectilePrefab, ImpactPrefab, turnRate: TurnRate, targetId: targetId, kind: DamageKind.Secondary));
            }
        }
    }
}
