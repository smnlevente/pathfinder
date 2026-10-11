using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Stinger (M7, galaxy 3): a quick Swarm insect that circles the ship at a distance, then stops short, shows a chevron
    /// lane through the ship and darts along it, overshooting, before it goes back to circling. Fragile, and it comes in
    /// from the side rather than head-on, so a brood of them keeps the pilot turning. It is vulnerable while it recovers.
    /// </summary>
    public sealed class StingerBrain : EnemyBrain, IChargeTelegraph
    {
        public enum State { Orbit, WindUp, Dash, Recover }

        [SerializeField, Min(1f)] float orbitRadius = 6f;
        [SerializeField, Min(0.1f)] float orbitTimeMin = 1.8f;
        [SerializeField, Min(0.1f)] float orbitTimeMax = 3.2f;
        [SerializeField, Min(0f), Tooltip("Only dashes when the ship is within this distance.")] float engageDistance = 11f;
        [SerializeField, Min(0f)] float windUpTime = 0.6f;
        [SerializeField, Min(0.1f)] float dashSpeed = 22f;
        [SerializeField, Min(0f), Tooltip("How far past the ship the dash carries it.")] float overshoot = 4f;
        [SerializeField, Min(1f)] float maxDashDistance = 14f;
        [SerializeField, Min(1f)] float dashContactMultiplier = 1.6f;
        [SerializeField, Min(0f)] float recoverTime = 0.6f;

        float _timer, _dashLength, _dashed;
        float _orbitDirection = 1f;
        Vector2 _dashStart, _lastPosition;

        public State Current { get; private set; }
        public Vector2 ChargeDirection { get; private set; } = Vector2.up;

        bool IChargeTelegraph.ShowsCharge => Current == State.WindUp;
        Vector2 IChargeTelegraph.ChargeStart => _dashStart;
        float IChargeTelegraph.ChargeLength => _dashLength;
        float IChargeTelegraph.ChargeProgress => Current == State.WindUp && windUpTime > 0f ? Mathf.Clamp01(1f - _timer / windUpTime) : 0f;

        public override void ResetState(Enemy self)
        {
            _orbitDirection = self.Random.Range(0, 2) == 0 ? -1f : 1f;
            Enter(self, State.Orbit, self.Random.Range(orbitTimeMin, orbitTimeMax));
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            float attackDelta = deltaTime * self.AttackRate;
            var target = Target(self);

            switch (Current)
            {
                case State.Orbit:
                {
                    if (target == null)
                        return Vector2.zero;
                    Vector2 toTarget = target.Position - self.Position;
                    float distance = toTarget.magnitude;
                    Vector2 inward = distance > 1e-4f ? toTarget / distance : Vector2.up;
                    Vector2 tangent = new Vector2(-inward.y, inward.x) * _orbitDirection;

                    _timer -= attackDelta;
                    if (_timer <= 0f && distance <= engageDistance)
                    {
                        ChargeDirection = inward;
                        _dashStart = self.Position;
                        _dashLength = Mathf.Min(distance + overshoot, maxDashDistance);
                        Enter(self, State.WindUp, windUpTime);
                        return Vector2.zero;
                    }
                    // Circle, pulling in or out towards the orbit.
                    float pull = Mathf.Clamp((distance - orbitRadius) * 0.5f, -1f, 1f);
                    return (tangent + inward * pull).normalized * self.MoveSpeed;
                }

                case State.WindUp:
                    self.FaceTowards(ChargeDirection);
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 6f;
                    _timer -= attackDelta;
                    if (_timer <= 0f)
                    {
                        _dashed = 0f;
                        _lastPosition = self.Position;
                        Enter(self, State.Dash, _dashLength / dashSpeed + 0.2f);
                    }
                    return Vector2.zero;

                case State.Dash:
                {
                    self.FaceTowards(ChargeDirection);
                    self.SpeedMultiplier = dashSpeed / Mathf.Max(self.MoveSpeed, 0.01f);
                    self.AccelerationMultiplier = 20f;
                    self.ContactDamageMultiplier = dashContactMultiplier;

                    float moved = (self.Position - _lastPosition).magnitude;
                    _lastPosition = self.Position;
                    _dashed += moved;
                    bool pinned = _dashed > 0.5f && moved < dashSpeed * deltaTime * 0.25f;
                    _timer -= deltaTime;
                    if (_timer <= 0f || _dashed >= _dashLength || self.DealtContactDamage || pinned)
                    {
                        Enter(self, State.Recover, recoverTime);
                        return Vector2.zero;
                    }
                    return ChargeDirection * dashSpeed;
                }

                default: // Recover
                    self.SpeedMultiplier = 0.25f;
                    self.AccelerationMultiplier = 3f;
                    _timer -= attackDelta;
                    if (_timer <= 0f)
                    {
                        _orbitDirection = self.Random.Range(0, 2) == 0 ? -1f : 1f;
                        Enter(self, State.Orbit, self.Random.Range(orbitTimeMin, orbitTimeMax));
                    }
                    return Vector2.zero;
            }
        }

        void Enter(Enemy self, State state, float duration)
        {
            Current = state;
            _timer = duration;
            self.IsTelegraphing = state == State.WindUp;
        }
    }
}
