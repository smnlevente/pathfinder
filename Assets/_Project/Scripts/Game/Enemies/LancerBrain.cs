using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Lancer: closes in, locks a direction and shows a chevron lane, then charges along it at high speed and hits
    /// hard. Each attack is a double joust: after the first charge it re-aims at the ship and charges again after a
    /// shorter wind-up. It is vulnerable while it recovers (GDD §7.3).
    /// </summary>
    public sealed class LancerBrain : EnemyBrain, IChargeTelegraph
    {
        public enum State { Approach, WindUp, Charge, Recover }

        [SerializeField, Min(0f), Tooltip("Starts winding up within this distance of the target.")] float engageDistance = 9f;
        [SerializeField, Min(0f)] float windUpTime = 0.7f;
        [SerializeField, Min(1)] int chargesPerAttack = 2;
        [SerializeField, Min(0f), Tooltip("Wind-up before each follow-up charge.")] float followUpWindUpTime = 0.45f;
        [SerializeField, Min(0.1f)] float chargeSpeed = 24f;
        [SerializeField, Min(0.1f)] float chargeDistance = 14f;
        [SerializeField, Min(1f)] float chargeContactMultiplier = 2.5f;
        [SerializeField, Min(0f)] float recoverTime = 0.9f;
        [SerializeField, Range(0f, 1f)] float recoverSpeedFactor = 0.3f;
        [SerializeField, Min(0f), Tooltip("Pause after recovering before the next wind-up.")] float cooldown = 1.2f;

        float _timer;
        float _windUpDuration;
        int _chargesLeft;
        float _cooldownLeft;
        float _charged;
        Vector2 _lastPosition;

        public State Current { get; private set; }

        /// <summary>Locked charge direction, valid during wind-up and charge.</summary>
        public Vector2 ChargeDirection { get; private set; } = Vector2.up;

        public float ChargeDistance => chargeDistance;

        /// <summary>How far through the current wind-up it is, 0 to 1.</summary>
        public float WindUpProgress => Current == State.WindUp && _windUpDuration > 0f ? Mathf.Clamp01(1f - _timer / _windUpDuration) : 0f;

        bool IChargeTelegraph.ShowsCharge => Current == State.WindUp;
        Vector2 IChargeTelegraph.ChargeStart => Pathfinder.Game.Simulation.PlaneMath.ToPlane(transform.position);
        float IChargeTelegraph.ChargeLength => chargeDistance;
        float IChargeTelegraph.ChargeProgress => WindUpProgress;

        public override void ResetState(Enemy self)
        {
            Current = State.Approach;
            _cooldownLeft = self.Random.Range(0.3f, cooldown);
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            float attackDelta = deltaTime * self.AttackRate;
            var target = Target(self);

            switch (Current)
            {
                case State.Approach:
                {
                    if (target == null)
                        return Vector2.zero;

                    Vector2 toTarget = target.Position - self.Position;
                    _cooldownLeft -= attackDelta;
                    if (_cooldownLeft <= 0f && toTarget.magnitude <= engageDistance)
                    {
                        _chargesLeft = chargesPerAttack;
                        WindUp(self, toTarget, windUpTime);
                        return Vector2.zero;
                    }
                    return toTarget.normalized * self.MoveSpeed;
                }

                case State.WindUp:
                    self.FaceTowards(ChargeDirection);
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 5f; // plant itself quickly so the telegraph line is accurate
                    if (Advance(attackDelta))
                    {
                        _charged = 0f;
                        _lastPosition = self.Position;
                        Enter(self, State.Charge, chargeDistance / chargeSpeed + 0.2f);
                    }
                    return Vector2.zero;

                case State.Charge:
                {
                    self.FaceTowards(ChargeDirection);
                    self.SpeedMultiplier = chargeSpeed / Mathf.Max(self.MoveSpeed, 0.01f);
                    self.AccelerationMultiplier = 20f;
                    self.ContactDamageMultiplier = chargeContactMultiplier;

                    // Stop after the full distance, on hitting the ship, or when pinned against a wall.
                    float moved = (self.Position - _lastPosition).magnitude;
                    _lastPosition = self.Position;
                    _charged += moved;
                    bool pinned = _charged > 0.5f && moved < chargeSpeed * deltaTime * 0.25f;
                    if (Advance(deltaTime) || _charged >= chargeDistance || self.DealtContactDamage || pinned)
                    {
                        // A hit ends the attack; a miss lets it re-aim and joust again.
                        _chargesLeft--;
                        if (_chargesLeft > 0 && !self.DealtContactDamage && target != null)
                            WindUp(self, target.Position - self.Position, followUpWindUpTime);
                        else
                            Enter(self, State.Recover, recoverTime);
                        return Vector2.zero;
                    }
                    return ChargeDirection * chargeSpeed;
                }

                default: // Recover
                    self.SpeedMultiplier = recoverSpeedFactor;
                    if (Advance(attackDelta))
                    {
                        _cooldownLeft = cooldown;
                        Enter(self, State.Approach, 0f);
                    }
                    return target != null ? (target.Position - self.Position).normalized * self.MoveSpeed : Vector2.zero;
            }
        }

        void WindUp(Enemy self, Vector2 toTarget, float duration)
        {
            ChargeDirection = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : ChargeDirection;
            _windUpDuration = duration;
            Enter(self, State.WindUp, duration);
        }

        void Enter(Enemy self, State state, float duration)
        {
            Current = state;
            _timer = duration;
            self.IsTelegraphing = state == State.WindUp;
        }

        bool Advance(float deltaTime)
        {
            _timer -= deltaTime;
            return _timer <= 0f;
        }
    }
}
