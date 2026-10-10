using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>
    /// The Stormbringer's trait: each primary hit has a chance to call lightning from above, damaging enemies around the
    /// impact (GDD §5.3).
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Lightning Core (trait)", fileName = "Trait_LightningCore")]
    public sealed class LightningCoreDefinition : EquipmentDefinition
    {
        [field: SerializeField, Range(0f, 1f)] public float Chance { get; private set; } = 0.08f;
        [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 25f;
        [field: SerializeField, Min(0.1f)] public float Radius { get; private set; } = 2f;
        [field: SerializeField] public GameObject StrikeEffect { get; private set; }

        static readonly List<CombatEntity> s_hits = new List<CombatEntity>();

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) =>
            Localization.Loc.T($"{Id}.desc", ("chance", Chance * 100f), ("damage", Damage), ("radius", Radius));

        sealed class Effect : EquipmentEffect
        {
            readonly LightningCoreDefinition _definition;

            public Effect(LightningCoreDefinition definition) => _definition = definition;

            public override void OnHit(CombatEntity target, in DamageInfo info, DamageResult result)
            {
                if (!Random.Chance(_definition.Chance))
                    return;

                Vector2 point = info.Point;
                s_hits.Clear();
                foreach (var enemy in CombatRegistry.OnTeam(Ship.Team.Opponent()))
                {
                    float reach = _definition.Radius + enemy.Radius;
                    if (enemy.IsAlive && !enemy.IsIntangible && !enemy.Armoured && (enemy.Position - point).sqrMagnitude <= reach * reach)
                        s_hits.Add(enemy);
                }
                foreach (var enemy in s_hits)
                    enemy.ApplyDamage(new DamageInfo(_definition.Damage, Ship.Team, Ship.EntityId, enemy.Position, Vector2.zero, DamageKind.Proc));

                if (_definition.StrikeEffect != null)
                    Object.Instantiate(_definition.StrikeEffect, PlaneMath.ToWorld(point), Quaternion.identity);
            }
        }
    }
}
