using System;

namespace Pathfinder.Core.Combat
{
    /// <summary>
    /// Dash charges, recharge and the active window (GDD §5.2). Charges recharge one at a time. The
    /// recharge starts as soon as a charge is spent, even while the dash is still active.
    /// </summary>
    public sealed class DashState
    {
        public int MaxCharges { get; }
        public float Duration { get; }
        public float RechargeTime { get; }

        public int Charges { get; private set; }
        public bool IsDashing => _activeRemaining > 0f;

        /// <summary>Progress of the charge that is recharging, from 0 to 1. 0 when all charges are full.</summary>
        public float RechargeProgress => Charges < MaxCharges ? _recharge / RechargeTime : 0f;

        float _activeRemaining;
        float _recharge;

        public DashState(int maxCharges, float duration, float rechargeTime)
        {
            if (maxCharges <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharges), "A ship needs at least one dash charge.");
            if (duration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), "Dash duration must be positive.");
            if (rechargeTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rechargeTime), "Recharge time must be positive.");

            MaxCharges = Charges = maxCharges;
            Duration = duration;
            RechargeTime = rechargeTime;
        }

        /// <summary>Spends a charge and starts the dash. Fails while already dashing or when out of charges.</summary>
        public bool TryStart()
        {
            if (IsDashing || Charges == 0)
                return false;

            Charges--;
            _activeRemaining = Duration;
            return true;
        }

        /// <summary>
        /// Call after the tick's movement has been applied, so a dash covers exactly
        /// <see cref="Duration"/> worth of ticks.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _activeRemaining -= deltaTime;
            if (_activeRemaining <= TimeTolerance.Epsilon)
                _activeRemaining = 0f;

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
