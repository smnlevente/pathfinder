using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Last Stand: once per level, a lethal hit leaves the ship at 1 hull and briefly untouchable (GDD §8.5).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Last Stand", fileName = "Equipment_LastStand")]
    public sealed class LastStandDefinition : EquipmentDefinition
    {
        [field: SerializeField] public float[] GraceSeconds { get; private set; } = { 0.5f, 1f, 1.5f };

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) =>
            Localization.Loc.T($"{Id}.desc", ("seconds", AtLevel(GraceSeconds, level)));

        sealed class Effect : EquipmentEffect
        {
            readonly LastStandDefinition _definition;
            bool _used;

            public Effect(LastStandDefinition definition) => _definition = definition;

            public override void OnLevelStart() => _used = false;

            public override bool TryPreventLethal()
            {
                if (_used)
                    return false;

                _used = true;
                Ship.GrantIntangibility(AtLevel(_definition.GraceSeconds, Level));
                return true;
            }
        }
    }
}
