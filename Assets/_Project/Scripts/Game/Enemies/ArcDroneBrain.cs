using Pathfinder.Core.Random;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Arc Drone: hovers at mid range and strafes. It locks onto where the ship is, shows a flickering line through that
    /// spot while its coils charge, then discharges a bolt of electricity along the line, instantly and out to its full
    /// range. Move off the line during the charge, or dash through the bolt (GDD §7.3).
    /// </summary>
    public sealed class ArcDroneBrain : EnemyBrain, IBeamTelegraph
    {
        public enum State { Hover, Charge }

        [Header("Positioning")]
        [SerializeField, Min(0f)] float preferredMinDistance = 6f;
        [SerializeField, Min(0f)] float preferredMaxDistance = 8.5f;
        [SerializeField, Range(0f, 1f)] float strafeSpeedFactor = 0.7f;
        [SerializeField, Min(0.1f)] float strafeSwitchMin = 1.2f;
        [SerializeField, Min(0.1f)] float strafeSwitchMax = 3f;

        [Header("Zap")]
        [SerializeField, Min(0f), Tooltip("Only starts charging when the target is this close.")] float attackRange = 11f;
        [SerializeField, Min(0.05f)] float chargeTime = 0.7f;
        [SerializeField, Min(0.1f)] float cooldown = 2.5f;
        [SerializeField, Min(0.1f)] float zapRange = 12f;
        [SerializeField, Min(0f)] float zapDamage = 14f;
        [SerializeField, Min(0f), Tooltip("Added to the ship's radius when testing the bolt against it.")] float zapHalfWidth = 0.3f;
        [SerializeField, Tooltip("A short-lived jagged bolt, given its end points.")] Presentation.ArcBolt boltPrefab;

        float _timer;
        float _strafeSign;
        float _strafeTimer;
        Vector2 _lockedDirection = Vector2.up;
        float _beamLength;
        Vector2 _position;

        public State Current { get; private set; }

        /// <summary>The direction locked when the charge began.</summary>
        public Vector2 LockedDirection => _lockedDirection;

        public float ZapRange => zapRange;

        bool IBeamTelegraph.ShowsBeam => Current == State.Charge;
        Vector2 IBeamTelegraph.BeamStart => _position;
        Vector2 IBeamTelegraph.BeamEnd => _position + _lockedDirection * _beamLength;
        float IBeamTelegraph.BeamProgress => chargeTime > 0f ? Mathf.Clamp01(1f - _timer / chargeTime) : 1f;

        public override void ResetState(Enemy self)
        {
            Current = State.Hover;
            _timer = self.Random.Range(0.8f, cooldown);
            _strafeSign = self.Random.Chance(0.5f) ? 1f : -1f;
            _strafeTimer = self.Random.Range(strafeSwitchMin, strafeSwitchMax);
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            _position = self.Position;
            var target = Target(self);
            float attackDelta = deltaTime * self.AttackRate;

            if (Current == State.Charge)
            {
                self.SpeedMultiplier = 0f;
                self.AccelerationMultiplier = 5f;
                self.FaceTowards(_lockedDirection);
                _timer -= attackDelta;
                if (_timer <= 0f)
                {
                    Zap(self, world);
                    Current = State.Hover;
                    self.IsTelegraphing = false;
                    _timer = cooldown + self.Random.Range(-0.4f, 0.4f);
                }
                return Vector2.zero;
            }

            if (target == null)
                return Vector2.zero;

            Vector2 toTarget = target.Position - self.Position;
            float distance = toTarget.magnitude;
            Vector2 forward = distance > 1e-4f ? toTarget / distance : Vector2.up;
            self.FaceTowards(forward);

            _timer -= attackDelta;
            if (_timer <= 0f && distance <= attackRange)
            {
                // Lock onto where the ship is now; it has the charge time to get off the line.
                _lockedDirection = forward;
                _beamLength = Mathf.Min(zapRange, DistanceToWall(world, self.Position, forward));
                Current = State.Charge;
                _timer = chargeTime;
                self.IsTelegraphing = true;
                return Vector2.zero;
            }

            _strafeTimer -= deltaTime;
            if (_strafeTimer <= 0f)
            {
                _strafeSign = -_strafeSign;
                _strafeTimer = self.Random.Range(strafeSwitchMin, strafeSwitchMax);
            }
            float radial = distance > preferredMaxDistance ? 1f : distance < preferredMinDistance ? -1f : 0f;
            Vector2 side = new Vector2(-forward.y, forward.x) * (_strafeSign * strafeSpeedFactor);
            return Vector2.ClampMagnitude(forward * radial + side, 1f) * self.MoveSpeed;
        }

        /// <summary>An instant bolt along the locked line. It stops at the first ship it hits; dashing ships phase through it.</summary>
        void Zap(Enemy self, CombatWorld world)
        {
            Vector2 start = self.Position + _lockedDirection * (self.Radius * 0.8f);
            Vector2 end = self.Position + _lockedDirection * Mathf.Min(zapRange, DistanceToWall(world, self.Position, _lockedDirection));
            Vector2 boltEnd = end;
            PlayerShip struck = null;
            float nearest = float.MaxValue;

            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive || !PlaneMath.SegmentHitsCircle(start, end, player.Position, player.Radius + zapHalfWidth, out float t))
                    continue;
                if (player.IsIntangible)
                {
                    player.NotifyPhasedThrough();
                    continue;
                }
                if (t < nearest)
                {
                    nearest = t;
                    struck = player;
                }
            }

            if (struck != null)
            {
                boltEnd = Vector2.Lerp(start, end, nearest);
                struck.ApplyDamage(new DamageInfo(zapDamage, self.Team, self.EntityId, boltEnd, _lockedDirection));
            }

            if (boltPrefab != null)
                Instantiate(boltPrefab).Strike(PlaneMath.ToWorld(start, ProjectileSystem.VisualHeight), PlaneMath.ToWorld(boltEnd, ProjectileSystem.VisualHeight));
            GameAudio.Play(SoundCue.Zap, transform.position);
        }

        /// <summary>The bolt grounds itself in the arena wall.</summary>
        static float DistanceToWall(CombatWorld world, Vector2 from, Vector2 direction)
        {
            Vector2 half = world.ArenaSize * 0.5f;
            float tx = Mathf.Abs(direction.x) > 1e-4f ? ((direction.x > 0f ? half.x : -half.x) - from.x) / direction.x : float.MaxValue;
            float ty = Mathf.Abs(direction.y) > 1e-4f ? ((direction.y > 0f ? half.y : -half.y) - from.y) / direction.y : float.MaxValue;
            return Mathf.Max(0f, Mathf.Min(tx, ty));
        }
    }
}
