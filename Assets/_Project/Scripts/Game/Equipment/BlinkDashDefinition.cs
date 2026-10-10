using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Blink Dash: teleport instantly instead of travelling, straight through enemies (GDD §8.2).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Blink Dash", fileName = "Equipment_BlinkDash")]
    public sealed class BlinkDashDefinition : EquipmentDefinition
    {
        [field: SerializeField] public float[] ExtraDistance { get; private set; } = { 0f, 1f, 2f };
        [field: SerializeField, Tooltip("Presentation-only effect at both ends of the blink.")] public GameObject BlinkEffect { get; private set; }

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level)
        {
            float extra = AtLevel(ExtraDistance, level);
            return extra > 0f ? Localization.Loc.T($"{Id}.desc.further", ("distance", extra)) : Localization.Loc.T($"{Id}.desc");
        }

        sealed class Effect : EquipmentEffect
        {
            readonly BlinkDashDefinition _definition;

            public Effect(BlinkDashDefinition definition) => _definition = definition;

            public override bool OverrideDashMovement(Vector2 direction)
            {
                Vector2 from = Ship.Position;
                Vector2 to = World.ClampToArena(from + direction * (Ship.DashDistance + AtLevel(_definition.ExtraDistance, Level)), Ship.Radius);
                Ship.BlinkTo(to);

                if (_definition.BlinkEffect != null)
                {
                    Object.Instantiate(_definition.BlinkEffect, PlaneMath.ToWorld(from, 0.5f), Quaternion.identity);
                    Object.Instantiate(_definition.BlinkEffect, PlaneMath.ToWorld(to, 0.5f), Quaternion.identity);
                }
                return true;
            }
        }
    }
}
