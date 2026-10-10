using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Incendiary: primary hits set enemies burning. Burns stack, up to a cap (GDD §8.3).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Incendiary", fileName = "Equipment_Incendiary")]
    public sealed class IncendiaryDefinition : EquipmentDefinition
    {
        [field: SerializeField] public float[] DamagePerSecond { get; private set; } = { 3f, 4f, 5f };
        [field: SerializeField, Min(0.1f)] public float Duration { get; private set; } = 3f;
        [field: SerializeField, Min(1), Tooltip("Burn stacks per enemy. Without a cap a fast weapon would melt everything.")] public int MaxStacks { get; private set; } = 5;

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) =>
            $"Primary hits burn for {AtLevel(DamagePerSecond, level):0} damage/s over {Duration:0} s (stacks up to {MaxStacks}×)";

        sealed class Effect : EquipmentEffect
        {
            readonly IncendiaryDefinition _definition;

            public Effect(IncendiaryDefinition definition) => _definition = definition;

            public override void OnHit(CombatEntity target, in DamageInfo info, DamageResult result)
            {
                if (target is Enemy enemy && enemy.IsAlive)
                    enemy.ApplyBurn(AtLevel(_definition.DamagePerSecond, Level), _definition.Duration, Ship.EntityId, _definition.MaxStacks);
            }
        }
    }
}
