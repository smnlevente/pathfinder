using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Kill Refund: each kill shortens the secondary weapon's remaining cooldown (GDD §8.4).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Kill Refund", fileName = "Equipment_KillRefund")]
    public sealed class KillRefundDefinition : EquipmentDefinition
    {
        [field: SerializeField] public float[] SecondsPerKill { get; private set; } = { 0.3f, 0.45f, 0.6f };

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) => Localization.Loc.T($"{Id}.desc", ("seconds", AtLevel(SecondsPerKill, level)));

        sealed class Effect : EquipmentEffect
        {
            readonly KillRefundDefinition _definition;

            public Effect(KillRefundDefinition definition) => _definition = definition;

            public override void OnKill(Enemy enemy, in DamageInfo info) => Ship.SecondaryCooldown?.Tick(AtLevel(_definition.SecondsPerKill, Level));
        }
    }
}
