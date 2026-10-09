using System;

namespace Pathfinder.Core.Combat
{
    /// <summary>
    /// Charges that recharge one at a time, as used by the dash and secondary weapons. Recharging starts as soon as
    /// a charge is spent.
    /// </summary>
    public sealed class Cooldown
    {
        public int MaxCharges { get; }
        public float RechargeTime { get; }
        public int Charges { get; private set; }

        public bool IsReady => Charges > 0;

        /// <summary>Progress of the charge that is recharging, from 0 to 1. 0 when all charges are full.</summary>
        public float RechargeProgress => Charges < MaxCharges ? _recharge / RechargeTime : 0f;

        /// <summary>Seconds until the next charge is ready, or 0 when one is available.</summary>
        public float TimeUntilReady => Charges > 0 ? 0f : Math.Max(0f, RechargeTime - _recharge);

        float _recharge;

        public Cooldown(int maxCharges, float rechargeTime)
        {
            if (maxCharges <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharges), "At least one charge is required.");
            if (rechargeTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rechargeTime), "Recharge time must be positive.");

            MaxCharges = Charges = maxCharges;
            RechargeTime = rechargeTime;
        }

        public bool TryUse()
        {
            if (Charges == 0)
                return false;

            Charges--;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (Charges >= MaxCharges)
                return;

            _recharge += deltaTime;
            while (_recharge >= RechargeTime - TimeTolerance.Epsilon && Charges < MaxCharges)
            {
                _recharge = Math.Max(0f, _recharge - RechargeTime);
                Charges++;
            }

            if (Charges == MaxCharges)
                _recharge = 0f;
        }
    }
}
