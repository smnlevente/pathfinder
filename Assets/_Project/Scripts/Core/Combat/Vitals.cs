using System;

namespace Pathfinder.Core.Combat
{
    /// <summary>
    /// Hull and shield damage model (GDD §5.1). Damage hits the shield first, then the hull. The hull
    /// never regenerates on its own. The shield regenerates after a delay without damage. A hull hit
    /// grants brief invulnerability so overlapping attacks can't delete the ship in one frame.
    /// </summary>
    public sealed class Vitals
    {
        public float MaxHull { get; private set; }
        public float Hull { get; private set; }
        public float MaxShield { get; private set; }
        public float Shield { get; private set; }

        /// <summary>Shield points restored per second once regeneration starts.</summary>
        public float ShieldRegenRate { get; set; }

        /// <summary>Seconds without taking damage before the shield starts regenerating.</summary>
        public float ShieldRegenDelay { get; set; }

        /// <summary>Invulnerability granted after any hit that reaches the hull.</summary>
        public float HitInvulnerability { get; set; }

        /// <summary>Multiplier on all incoming damage, e.g. 0.9 for Titan or 1.25 for Vampiric (GDD §5.3).</summary>
        public float DamageTakenMultiplier { get; set; } = 1f;

        /// <summary>Invulnerability from an outside source, such as dash i-frames. The owner sets it every tick.</summary>
        public bool ExternalInvulnerability { get; set; }

        public bool IsDead => Hull <= 0f;
        public bool IsInvulnerable => _hitInvulnerabilityRemaining > 0f || ExternalInvulnerability;

        /// <summary>Only the brief invulnerability after a hull hit, not outside sources such as dash i-frames.</summary>
        public bool HasHitInvulnerability => _hitInvulnerabilityRemaining > 0f;

        float _hitInvulnerabilityRemaining;
        float _timeSinceDamage;

        public Vitals(float maxHull, float maxShield = 0f)
        {
            if (maxHull <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxHull), "Max hull must be positive.");
            if (maxShield < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxShield), "Max shield cannot be negative.");

            MaxHull = Hull = maxHull;
            MaxShield = Shield = maxShield;
        }

        public DamageResult ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return default;
            if (IsInvulnerable)
                return DamageResult.BlockedResult;

            amount *= DamageTakenMultiplier;
            float toShield = Math.Min(Shield, amount);
            float toHull = Math.Min(Hull, amount - toShield);

            Shield -= toShield;
            Hull -= toHull;
            _timeSinceDamage = 0f;

            if (toHull > 0f)
                _hitInvulnerabilityRemaining = HitInvulnerability;

            return new DamageResult(toShield, toHull, killed: IsDead, blocked: false);
        }

        /// <summary>Restores hull and returns the amount actually healed. Has no effect on a destroyed ship.</summary>
        public float Heal(float amount)
        {
            if (IsDead || amount <= 0f)
                return 0f;

            float healed = Math.Min(amount, MaxHull - Hull);
            Hull += healed;
            return healed;
        }

        /// <summary>
        /// Sets new maximums, e.g. after equipment changes. A higher maximum also heals by the difference, and a lower one
        /// clamps the current value.
        /// </summary>
        public void SetLimits(float maxHull, float maxShield)
        {
            if (IsDead)
                return;

            maxHull = Math.Max(1f, maxHull);
            maxShield = Math.Max(0f, maxShield);
            Hull = Math.Min(maxHull, Hull + Math.Max(0f, maxHull - MaxHull));
            Shield = Math.Min(maxShield, Shield + Math.Max(0f, maxShield - MaxShield));
            MaxHull = maxHull;
            MaxShield = maxShield;
        }

        public void AddShield(float amount)
        {
            if (!IsDead && amount > 0f)
                Shield = Math.Min(MaxShield, Shield + amount);
        }

        /// <summary>Brings a ship back from a lethal hit, e.g. Last Stand (GDD §8.5).</summary>
        public void Revive(float hull) => Hull = Math.Max(1f, Math.Min(MaxHull, hull));

        /// <summary>
        /// Takes hull directly, past the shield and any invulnerability, but never below <paramref name="floor"/>: for
        /// costs such as a Mystery event's, which must never end a run outright. Returns the hull lost.
        /// </summary>
        public float DrainHull(float amount, float floor = 1f)
        {
            float target = Math.Max(Math.Min(Hull, floor), Hull - Math.Max(0f, amount));
            float lost = Hull - target;
            Hull = target;
            return lost;
        }

        /// <summary>
        /// Saps the shield without a hit: no invulnerability, no hit feedback, but it counts as damage for the regeneration
        /// delay, so a drained shield stays down. E.g. a spore cloud. Returns the shield lost.
        /// </summary>
        public float DrainShield(float amount)
        {
            if (IsDead || amount <= 0f || Shield <= 0f)
                return 0f;
            float lost = Math.Min(Shield, amount);
            Shield -= lost;
            _timeSinceDamage = 0f;
            return lost;
        }

        /// <summary>Raises max hull, e.g. Repair's "Reinforce" option (GDD §10.4). Optionally heals by the same amount.</summary>
        public void IncreaseMaxHull(float amount, bool heal)
        {
            if (amount <= 0f || IsDead)
                return;

            MaxHull += amount;
            if (heal)
                Hull += amount;
        }

        public void Tick(float deltaTime)
        {
            if (IsDead)
                return;

            _hitInvulnerabilityRemaining = Math.Max(0f, _hitInvulnerabilityRemaining - deltaTime);
            _timeSinceDamage += deltaTime;

            if (_timeSinceDamage >= ShieldRegenDelay - TimeTolerance.Epsilon && Shield < MaxShield)
                Shield = Math.Min(MaxShield, Shield + ShieldRegenRate * deltaTime);
        }
    }

    public readonly struct DamageResult
    {
        internal static readonly DamageResult BlockedResult = new DamageResult(0f, 0f, killed: false, blocked: true);

        public float ShieldDamage { get; }
        public float HullDamage { get; }
        public bool Killed { get; }

        /// <summary>The hit landed while the target was invulnerable.</summary>
        public bool Blocked { get; }

        public float TotalDamage => ShieldDamage + HullDamage;

        public DamageResult(float shieldDamage, float hullDamage, bool killed, bool blocked)
        {
            ShieldDamage = shieldDamage;
            HullDamage = hullDamage;
            Killed = killed;
            Blocked = blocked;
        }
    }
}
