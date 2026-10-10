using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Pushback Dash: a shockwave at the dash end shoves and damages nearby enemies (GDD §8.2).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Pushback Dash", fileName = "Equipment_PushbackDash")]
    public sealed class PushbackDashDefinition : EquipmentDefinition
    {
        [field: SerializeField] public float[] Radius { get; private set; } = { 3f, 3.5f, 4f };
        [field: SerializeField] public float[] Damage { get; private set; } = { 10f, 20f, 30f };
        [field: SerializeField, Min(0f)] public float Knockback { get; private set; } = 14f;
        [field: SerializeField] public GameObject ShockwaveEffect { get; private set; }

        static readonly List<Enemy> s_hits = new List<Enemy>();

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) =>
            Localization.Loc.T($"{Id}.desc", ("radius", AtLevel(Radius, level)), ("damage", AtLevel(Damage, level)));

        sealed class Effect : EquipmentEffect
        {
            readonly PushbackDashDefinition _definition;

            public Effect(PushbackDashDefinition definition) => _definition = definition;

            public override void OnDashEnd()
            {
                float radius = AtLevel(_definition.Radius, Level);
                s_hits.Clear();
                foreach (var enemy in CombatRegistry.Enemies)
                {
                    float reach = radius + enemy.Radius;
                    if (enemy.IsAlive && (enemy.Position - Ship.Position).sqrMagnitude <= reach * reach)
                        s_hits.Add(enemy);
                }

                foreach (var enemy in s_hits)
                {
                    Vector2 away = enemy.Position - Ship.Position;
                    Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : Ship.AimDirection;
                    enemy.ApplyDamage(new DamageInfo(AtLevel(_definition.Damage, Level), Ship.Team, Ship.EntityId, enemy.Position, direction, DamageKind.Proc));
                    if (enemy.IsAlive)
                        enemy.ApplyKnockback(direction * _definition.Knockback);
                }

                if (_definition.ShockwaveEffect != null)
                {
                    var effect = Object.Instantiate(_definition.ShockwaveEffect, PlaneMath.ToWorld(Ship.Position, 0.5f), Quaternion.identity);
                    if (effect.TryGetComponent<Pathfinder.Game.Presentation.RingPulse>(out var pulse))
                        pulse.SizeMultiplier = radius / 4f; // the effect is authored for a 4 u radius
                }
            }
        }
    }
}
