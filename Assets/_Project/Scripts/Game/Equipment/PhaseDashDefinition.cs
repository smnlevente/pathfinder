using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Phase Dash: the dash i-frames last longer, and phasing through bullets restores shield (GDD §8.2).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Phase Dash", fileName = "Equipment_PhaseDash")]
    public sealed class PhaseDashDefinition : EquipmentDefinition
    {
        [field: SerializeField] public float[] ExtraSeconds { get; private set; } = { 0.2f, 0.3f, 0.4f };
        [field: SerializeField, Min(0f)] public float ShieldPerBullet { get; private set; } = 2f;

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) =>
            $"Dash i-frames last {AtLevel(ExtraSeconds, level):0.#} s longer. Each bullet phased through restores {ShieldPerBullet:0} shield";

        sealed class Effect : EquipmentEffect
        {
            readonly PhaseDashDefinition _definition;

            public Effect(PhaseDashDefinition definition) => _definition = definition;

            public override float ExtraDashIntangibility => AtLevel(_definition.ExtraSeconds, Level);

            public override void OnPhasedThrough() => Ship.Vitals.AddShield(_definition.ShieldPerBullet);
        }
    }
}
