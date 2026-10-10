using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Ricochet: primary hits spawn a homing bounce toward another nearby enemy (GDD §8.3).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Ricochet", fileName = "Equipment_Ricochet")]
    public sealed class RicochetDefinition : EquipmentDefinition
    {
        [field: SerializeField] public float[] BounceDamage { get; private set; } = { 0.5f, 0.75f, 1f };
        [field: SerializeField, Min(0.5f)] public float SearchRadius { get; private set; } = 7f;

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) =>
            Localization.Loc.T($"{Id}.desc", ("percent", AtLevel(BounceDamage, level) * 100f));

        sealed class Effect : EquipmentEffect
        {
            readonly RicochetDefinition _definition;

            public Effect(RicochetDefinition definition) => _definition = definition;

            public override void OnHit(CombatEntity target, in DamageInfo info, DamageResult result)
            {
                CombatEntity next = null;
                float best = _definition.SearchRadius * _definition.SearchRadius;
                foreach (var enemy in CombatRegistry.OnTeam(Ship.Team.Opponent()))
                {
                    float distance = (enemy.Position - target.Position).sqrMagnitude;
                    if (enemy != target && enemy.IsAlive && !enemy.IsIntangible && !enemy.Armoured && distance < best)
                    {
                        best = distance;
                        next = enemy;
                    }
                }
                if (next == null)
                    return;

                // The bounce deals Proc damage, so it can't ricochet again.
                var weapon = Ship.PrimaryWeapon;
                Vector2 direction = (next.Position - info.Point).normalized;
                World.Projectiles.Spawn(new ProjectileSpawn(info.Point + direction * 0.3f, direction, weapon.ProjectileSpeed,
                    info.Amount * AtLevel(_definition.BounceDamage, Level), weapon.ProjectileRadius, _definition.SearchRadius * 1.5f,
                    Ship.Team, Ship.EntityId, weapon.ProjectilePrefab, weapon.ImpactPrefab, turnRate: 720f, targetId: next.EntityId,
                    kind: DamageKind.Proc));
            }
        }
    }
}
