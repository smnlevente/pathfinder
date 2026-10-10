using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>Split Shot: every few trigger pulls fires a 3-projectile fan (GDD §8.3).</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Split Shot", fileName = "Equipment_SplitShot")]
    public sealed class SplitShotDefinition : EquipmentDefinition
    {
        [field: SerializeField, Tooltip("Every Nth shot splits, at levels I, II, III.")] public int[] Interval { get; private set; } = { 4, 3, 2 };
        [field: SerializeField, Min(0f)] public float FanDegrees { get; private set; } = 12f;

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level) => Localization.Loc.T($"{Id}.desc", ("ordinal", Ordinal(IntervalAt(level))));

        int IntervalAt(int level) => Interval[Mathf.Clamp(level - 1, 0, Interval.Length - 1)];

        static string Ordinal(int n) => Localization.Loc.Has($"ordinal.{n}") ? Localization.Loc.T($"ordinal.{n}") : Localization.Loc.T("ordinal.n", ("n", n));

        sealed class Effect : EquipmentEffect
        {
            readonly SplitShotDefinition _definition;

            public Effect(SplitShotDefinition definition) => _definition = definition;

            public override void OnShotFired(ref ShotParams shot)
            {
                if (shot.ShotIndex % Mathf.Max(1, _definition.IntervalAt(Level)) != 0)
                    return;
                shot.ExtraProjectiles += 2;
                shot.ExtraSpreadDegrees = Mathf.Max(shot.ExtraSpreadDegrees, _definition.FanDegrees);
            }
        }
    }
}
