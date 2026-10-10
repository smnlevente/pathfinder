using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Bullet Dash: fires a ring of primary projectiles at the dash end (GDD §8.2).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Bullet Dash", fileName = "Equipment_BulletDash")]
    public sealed class BulletDashDefinition : EquipmentDefinition
    {
        [field: SerializeField] public int[] Projectiles { get; private set; } = { 8, 12, 16 };

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) => Localization.Loc.T($"{Id}.desc", ("count", CountAt(level)));

        int CountAt(int level) => Projectiles[Mathf.Clamp(level - 1, 0, Projectiles.Length - 1)];

        sealed class Effect : EquipmentEffect
        {
            readonly BulletDashDefinition _definition;

            public Effect(BulletDashDefinition definition) => _definition = definition;

            public override void OnDashEnd()
            {
                // The ring uses the equipped primary's projectile, so a Plasma Cutter rings with pellets and so on.
                var weapon = Ship.PrimaryWeapon;
                int count = _definition.CountAt(Level);
                float damage = weapon.Damage * Ship.PrimaryDamageMultiplier;
                for (int i = 0; i < count; i++)
                {
                    Vector2 direction = PlaneMath.FromHeading(i * 360f / count);
                    World.Projectiles.Spawn(new ProjectileSpawn(Ship.Position + direction * 0.6f, direction, weapon.ProjectileSpeed, damage,
                        weapon.ProjectileRadius, weapon.Range, Ship.Team, Ship.EntityId, weapon.ProjectilePrefab, weapon.ImpactPrefab,
                        pierce: weapon.Pierce, damageAtMaxRange: weapon.DamageAtMaxRange));
                }
            }
        }
    }
}
