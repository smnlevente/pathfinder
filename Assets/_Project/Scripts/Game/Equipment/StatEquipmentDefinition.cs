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
                if (Math.Abs(flat) > 1e-4f && bonus.Stat == StatId.CritChance)
                    parts.Add($"{Signed(flat * 100f, "0")}% {StatNames.Name(bonus.Stat)}"); // crit chance is a flat probability
                else if (Math.Abs(flat) > 1e-4f)
                    parts.Add($"{Signed(flat, "0.#")}{StatNames.Unit(bonus.Stat)} {StatNames.Name(bonus.Stat)}");
                if (Math.Abs(percent) > 1e-4f)
                    parts.Add($"{Signed(percent * 100f, "0")}% {StatNames.Name(bonus.Stat)}");
            }
            return string.Join(", ", parts);
        }

        static string Signed(float value, string format) => (value >= 0f ? "+" : "−") + Math.Abs(value).ToString(format);

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

    /// <summary>Player-facing stat names for generated descriptions.</summary>
    public static class StatNames
    {
        public static string Name(StatId stat) => stat switch
        {
            StatId.MaxHull => "max hull",
            StatId.MaxShield => "shield",
            StatId.ShieldRegenDelay => "shield regen delay",
            StatId.MoveSpeed => "move speed",
            StatId.MagnetRadius => "pickup magnet radius",
            StatId.DashCharges => "dash charges",
            StatId.DashDistance => "dash distance",
            StatId.DamageTaken => "damage taken",
            StatId.FireRate => "fire rate",
            StatId.MagazineSize => "magazine size",
            StatId.ProjectileDamage => "primary damage",
            StatId.Pierce => "pierce",
            StatId.CritChance => "critical hit chance",
            StatId.SecondaryCooldown => "secondary cooldown",
            StatId.SecondaryDamage => "secondary damage",
            StatId.SecondaryCharges => "secondary charges",
            StatId.ScrapsGain => "Scraps gained",
            _ => stat.ToString(),
        };

        public static string Unit(StatId stat) => stat switch
        {
            StatId.ShieldRegenDelay => " s",
            StatId.MagnetRadius => " u",
            StatId.DashDistance => " u",
            _ => string.Empty,
        };
    }
}
