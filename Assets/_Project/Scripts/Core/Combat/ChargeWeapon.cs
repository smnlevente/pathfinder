using System;

namespace Pathfinder.Core.Combat
{
    /// <summary>
    /// Hold-to-charge, release-to-fire cycle, as used by the Quantum Charger (GDD §6.1). A shot needs a
    /// minimum charge. Holding at full charge for too long overheats the weapon, cancelling the shot and
    /// locking it out. After a lockout the trigger must be released before charging can start again.
    /// </summary>
    public sealed class ChargeWeapon
    {
        public float MinChargeTime { get; }
        public float FullChargeTime { get; }
        public float OverheatTime { get; }
        public float LockoutTime { get; }

        /// <summary>Seconds the trigger has been held for the current charge.</summary>
        public float ChargeTime { get; private set; }

        public bool IsCharging => ChargeTime > 0f;
        public bool IsLockedOut => _lockoutRemaining > 0f;

        /// <summary>0 when idle, 1 at full charge. For HUDs and visuals.</summary>
        public float ChargeFraction => Math.Min(1f, ChargeTime / FullChargeTime);

        /// <summary>How close a held full charge is to overheating, from 0 to 1.</summary>
        public float HeatFraction => OverheatTime > 0f ? Math.Max(0f, Math.Min(1f, (ChargeTime - FullChargeTime) / OverheatTime)) : 0f;

        float _lockoutRemaining;
        bool _awaitRelease;

        public ChargeWeapon(float minChargeTime, float fullChargeTime, float overheatTime, float lockoutTime)
        {
            if (fullChargeTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fullChargeTime), "Full charge time must be positive.");
            if (minChargeTime < 0f || minChargeTime > fullChargeTime)
                throw new ArgumentOutOfRangeException(nameof(minChargeTime), "Minimum charge must be between 0 and the full charge time.");
            if (overheatTime < 0f || lockoutTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(overheatTime), "Overheat and lockout times cannot be negative.");

            MinChargeTime = minChargeTime;
            FullChargeTime = fullChargeTime;
            OverheatTime = overheatTime;
            LockoutTime = lockoutTime;
        }

        /// <summary>
        /// Advances one tick. Returns true when a shot is released. <paramref name="power"/> is then 0 at the
        /// minimum charge and 1 at full charge.
        /// </summary>
        public bool Tick(float deltaTime, bool triggerHeld, out float power)
        {
            power = 0f;

            if (_lockoutRemaining > 0f)
            {
                _lockoutRemaining -= deltaTime;
                if (_lockoutRemaining <= TimeTolerance.Epsilon)
                    _lockoutRemaining = 0f;
                return false;
            }

            if (_awaitRelease)
            {
                if (!triggerHeld)
                    _awaitRelease = false;
                return false;
            }

            if (triggerHeld)
            {
                ChargeTime += deltaTime;
                if (OverheatTime > 0f && ChargeTime >= FullChargeTime + OverheatTime - TimeTolerance.Epsilon)
                {
                    ChargeTime = 0f;
                    _lockoutRemaining = LockoutTime;
                    _awaitRelease = true;
                }
                return false;
            }

            if (ChargeTime <= 0f)
                return false;

            bool fires = ChargeTime >= MinChargeTime - TimeTolerance.Epsilon;
            if (fires)
            {
                float span = FullChargeTime - MinChargeTime;
                power = span > 0f ? Math.Min(1f, Math.Max(0f, (ChargeTime - MinChargeTime) / span)) : 1f;
            }

            ChargeTime = 0f;
            return fires;
        }
    }
}
