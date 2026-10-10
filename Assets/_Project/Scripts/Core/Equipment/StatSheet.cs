using System;
using System.Collections.Generic;

namespace Pathfinder.Core.Equipment
{
    /// <summary>Ship and weapon stats that equipment, traits and run effects can modify (GDD §14.2).</summary>
    public enum StatId
    {
        MaxHull,
        MaxShield,
        ShieldRegenDelay,
        MoveSpeed,
        MagnetRadius,
        DashCharges,
        DashDistance,
        DamageTaken,
        FireRate,
        MagazineSize,
        ProjectileDamage,
        Pierce,
        CritChance,
        SecondaryCooldown,
        SecondaryDamage,
        SecondaryCharges,
        ScrapsGain,
    }

    public readonly struct StatModifier
    {
        public StatId Stat { get; }

        /// <summary>Added to the base value.</summary>
        public float Flat { get; }

        /// <summary>Fraction added to the multiplier, so 0.15 means +15%. Percentages from different sources add up.</summary>
        public float Percent { get; }

        /// <summary>Who added the modifier, so a source can remove everything it added.</summary>
        public object Source { get; }

        public StatModifier(StatId stat, float flat, float percent, object source)
        {
            Stat = stat;
            Flat = flat;
            Percent = percent;
            Source = source;
        }
    }

    /// <summary>
    /// Stat modifiers from every source. A stat's value is <c>(base + Σ flat) × (1 + Σ percent)</c>, never below zero
    /// (GDD §14.2: base, then flat bonuses, then percentage bonuses). Gameplay code asks the sheet rather than
    /// hard-coding item effects.
    /// </summary>
    public sealed class StatSheet
    {
        readonly List<StatModifier> _modifiers = new List<StatModifier>();

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public void Add(StatModifier modifier) => _modifiers.Add(modifier);

        public void Add(StatId stat, float flat, float percent, object source) => _modifiers.Add(new StatModifier(stat, flat, percent, source));

        public int RemoveSource(object source) => _modifiers.RemoveAll(m => Equals(m.Source, source));

        public void Clear() => _modifiers.Clear();

        public float Get(StatId stat, float baseValue)
        {
            float flat = 0f, percent = 0f;
            foreach (var modifier in _modifiers)
            {
                if (modifier.Stat != stat)
                    continue;
                flat += modifier.Flat;
                percent += modifier.Percent;
            }
            return Math.Max(0f, (baseValue + flat) * (1f + percent));
        }

        /// <summary>For integer stats such as magazine size, charges or pierce. Rounded to the nearest integer.</summary>
        public int GetInt(StatId stat, int baseValue) => (int)Math.Round(Get(stat, baseValue), MidpointRounding.AwayFromZero);
    }
}
