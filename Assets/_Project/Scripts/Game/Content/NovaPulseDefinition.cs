using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>Nova Pulse: a shockwave that damages and knocks back nearby enemies and erases their bullets.</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Secondary/Nova Pulse", fileName = "Secondary_NovaPulse")]
    public sealed class NovaPulseDefinition : SecondaryWeaponDefinition
    {
        [field: SerializeField, Min(0.1f)] public float Radius { get; private set; } = 4f;
        [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 40f;
        [field: SerializeField, Min(0f), Tooltip("Outward speed given to enemies, in units per second.")] public float Knockback { get; private set; } = 14f;

        static readonly List<Enemy> s_hits = new List<Enemy>();

        public override void Activate(PlayerShip ship, CombatWorld world, IRandom random)
        {
            world.Projectiles.DestroyInRadius(Team.Enemy, ship.Position, Radius);

            // Collect first: damage can kill enemies, which changes the registry.
            s_hits.Clear();
            foreach (var enemy in CombatRegistry.Enemies)
            {
                float reach = Radius + enemy.Radius;
                if (enemy.IsAlive && (enemy.Position - ship.Position).sqrMagnitude <= reach * reach)
                    s_hits.Add(enemy);
            }

            foreach (var enemy in s_hits)
            {
                Vector2 away = enemy.Position - ship.Position;
                Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : ship.AimDirection;
                enemy.ApplyDamage(new DamageInfo(Damage * ship.SecondaryDamageMultiplier, ship.Team, ship.EntityId, enemy.Position - direction * enemy.Radius, direction, DamageKind.Secondary));
                if (enemy.IsAlive)
                    enemy.ApplyKnockback(direction * Knockback);
            }
        }
    }
}
