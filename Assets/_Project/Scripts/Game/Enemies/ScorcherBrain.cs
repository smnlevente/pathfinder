using Pathfinder.Core.Random;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Scorcher: a flamethrower grunt. It closes to short range, plants itself and marks the cone it is about to burn,
    /// then sprays a stream of short-lived flames across it, turning slowly after the ship, and cools down before the next
    /// spray. Its flames are a hazard to stay out of, not bullets to weave through (GDD §7.3).
    /// </summary>
    public sealed class ScorcherBrain : EnemyBrain, IAreaTelegraph
    {
        public enum State { Approach, WindUp, Spray, Cooldown }

        [SerializeField, Min(0f), Tooltip("Starts winding up within this distance of the target.")] float engageDistance = 4.5f;
        [SerializeField, Min(0f)] float windUpTime = 0.6f;
        [SerializeField, Min(0.1f)] float sprayTime = 1.4f;
        [SerializeField, Min(0f)] float cooldownTime = 2.2f;
        [SerializeField, Min(0f), Tooltip("Degrees per second it turns after the ship while winding up and spraying.")] float aimTurnRate = 60f;
        [SerializeField, Range(0f, 1f), Tooltip("Move speed multiplier while spraying.")] float sprayMoveFactor = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Move speed multiplier while cooling down.")] float cooldownMoveFactor = 0.5f;

        [Header("Flames")]
        [SerializeField, Min(0.02f)] float flameInterval = 0.06f;
        [SerializeField, Min(0f)] float flameDamage = 4f;
        [SerializeField, Min(0.1f)] float flameSpeed = 9f;
        [SerializeField, Min(0.1f)] float flameRange = 5f;
        [SerializeField, Min(0.01f)] float flameRadius = 0.4f;
        [SerializeField, Min(0f), Tooltip("Half-angle of the cone, in degrees.")] float spreadDegrees = 18f;
        [SerializeField, Min(0f)] float nozzleDistance = 0.6f;
        [SerializeField, Min(1f), Tooltip("Flames billow to this many times their size by the end of their range.")] float flameGrowth = 2.4f;
        [SerializeField] GameObject flamePrefab;

        float _timer;
        float _flameTimer;

        public State Current { get; private set; }

        bool IAreaTelegraph.ShowsArea => Current == State.WindUp;
        float IAreaTelegraph.AreaRadius => nozzleDistance + flameRange;
        float IAreaTelegraph.AreaArc => spreadDegrees * 2f + 6f;
        float IAreaTelegraph.AreaProgress => windUpTime > 0f ? Mathf.Clamp01(1f - _timer / windUpTime) : 1f;

        public override void ResetState(Enemy self)
        {
            Current = State.Cooldown;
            _timer = self.Random.Range(0.3f, cooldownTime * 0.5f);
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            var target = Target(self);
            float attackDelta = deltaTime * self.AttackRate;
            Vector2 toTarget = target != null ? target.Position - self.Position : Vector2.zero;
            Vector2 chase = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized * self.MoveSpeed : Vector2.zero;

            switch (Current)
            {
                case State.Approach:
                    if (target != null && toTarget.magnitude <= engageDistance)
                    {
                        Enter(self, State.WindUp, windUpTime);
                        self.FaceTowards(toTarget);
                        return Vector2.zero;
                    }
                    return chase;

                case State.WindUp:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 5f;
                    self.TurnTowards(toTarget, aimTurnRate * deltaTime);
                    _timer -= attackDelta;
                    if (_timer <= 0f)
                    {
                        _flameTimer = 0f;
                        Enter(self, State.Spray, sprayTime);
                        GameAudio.Play(SoundCue.Flame, transform.position);
                    }
                    return Vector2.zero;

                case State.Spray:
                    self.SpeedMultiplier = sprayMoveFactor;
                    self.TurnTowards(toTarget, aimTurnRate * deltaTime);
                    _flameTimer -= attackDelta;
                    while (_flameTimer <= 0f)
                    {
                        _flameTimer += flameInterval;
                        EmitFlame(self, world);
                    }
                    _timer -= attackDelta;
                    if (_timer <= 0f)
                        Enter(self, State.Cooldown, cooldownTime + self.Random.Range(-0.3f, 0.3f));
                    return chase;

                default: // Cooldown
                    self.SpeedMultiplier = cooldownMoveFactor;
                    _timer -= attackDelta;
                    if (_timer <= 0f)
                        Enter(self, State.Approach, 0f);
                    return chase;
            }
        }

        void EmitFlame(Enemy self, CombatWorld world)
        {
            Vector2 forward = PlaneMath.FromHeading(self.Heading);
            Vector2 direction = PlaneMath.Rotate(forward, self.Random.Range(-spreadDegrees, spreadDegrees));
            float speed = flameSpeed * self.Random.Range(0.85f, 1.15f);
            // Flames wash over the ship rather than stopping at it: each can burn it once, then carries on (post-hit
            // invulnerability paces the damage), so the stream reads as a cone of fire.
            world.Projectiles.Spawn(new ProjectileSpawn(self.Position + forward * nozzleDistance, direction, speed, flameDamage, flameRadius,
                flameRange, self.Team, self.EntityId, flamePrefab, pierce: 2, visualScale: self.Random.Range(0.6f, 0.9f), quiet: true,
                visualGrowth: flameGrowth));
        }

        void Enter(Enemy self, State state, float duration)
        {
            Current = state;
            _timer = duration;
            self.IsTelegraphing = state == State.WindUp;
        }
    }
}
