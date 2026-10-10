using System;
using System.Collections.Generic;
using Pathfinder.Core.Equipment;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>
    /// An item that only changes stats, configured as data: Overclock, Armor Plating, Rapid Cycler and so on (GDD §8.3–8.5).
    /// Its description is generated from the bonuses, so it can never drift from what the item actually does.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Stat Item", fileName = "Equipment_")]
    public sealed class StatEquipmentDefinition : EquipmentDefinition
    {
        [Serializable]
        public struct StatBonus
        {
            public StatId Stat;
            [Tooltip("Flat bonus at levels I, II, III.")] public float[] Flat;
            [Tooltip("Percentage bonus at levels I, II, III (0.15 = +15%).")] public float[] Percent;
        }

        [field: SerializeField] public StatBonus[] Bonuses { get; private set; } = new StatBonus[0];

        public override EquipmentEffect CreateEffect() => new Effect(this);

        public override string Describe(int level)
        {
            var parts = new List<string>();
            foreach (var bonus in Bonuses)
            {
                float flat = AtLevel(bonus.Flat, level);
                float percent = AtLevel(bonus.Percent, level);
                string stat = StatNames.Name(bonus.Stat);
                if (Math.Abs(flat) > 1e-4f && bonus.Stat == StatId.CritChance) // crit chance is a flat probability
                    parts.Add(Localization.Loc.T("stat.part.percent", ("signed", Signed(flat * 100f, "0")), ("stat", stat)));
                else if (Math.Abs(flat) > 1e-4f)
                    parts.Add(Localization.Loc.T("stat.part.flat", ("signed", Signed(flat, "0.#")), ("unit", StatNames.Unit(bonus.Stat)), ("stat", stat)));
                if (Math.Abs(percent) > 1e-4f)
                    parts.Add(Localization.Loc.T("stat.part.percent", ("signed", Signed(percent * 100f, "0")), ("stat", stat)));
            }
            return string.Join(Localization.Loc.T("stat.separator"), parts);
        }

        /// <summary>A number with its sign: +15, or −10 with a true minus sign.</summary>
        public static string Signed(float value, string format) =>
            (value >= 0f ? "+" : "−") + Math.Abs(value).ToString(format, System.Globalization.CultureInfo.InvariantCulture);

        sealed class Effect : EquipmentEffect
        {
            readonly StatEquipmentDefinition _definition;

            public Effect(StatEquipmentDefinition definition) => _definition = definition;

            public override void ApplyStats(StatSheet stats)
            {
                foreach (var bonus in _definition.Bonuses)
                    stats.Add(bonus.Stat, AtLevel(bonus.Flat, Level), AtLevel(bonus.Percent, Level), this);
            }
        }
    }

    /// <summary>Player-facing stat names for generated descriptions: <c>stat.{StatId}</c> in the .loc files.</summary>
    public static class StatNames
    {
        public static string Name(StatId stat) => Localization.Loc.TOr($"stat.{stat}", stat.ToString());

        public static string Unit(StatId stat) => stat switch
        {
            StatId.ShieldRegenDelay => Localization.Loc.T("unit.seconds"),
            StatId.MagnetRadius => Localization.Loc.T("unit.units"),
            StatId.DashDistance => Localization.Loc.T("unit.units"),
            _ => string.Empty,
        };
    }
}
