using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Crusher: a slow, heavy drone that shrugs off knockback. When it gets close it rears up, a red ring marking its
    /// reach, and slams down: a shockwave that hits very hard. Then it needs a moment to recover, the time to hurt it.
    /// Keep your distance, or dash out of the ring (GDD §7.3).
    /// </summary>
    public sealed class CrusherBrain : EnemyBrain, IAreaTelegraph
    {
        public enum State { Approach, WindUp, Recover }

        [SerializeField, Min(0f), Tooltip("Starts the slam when the target's centre is this close.")] float triggerDistance = 2.6f;
        [SerializeField, Min(0.05f)] float windUpTime = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("Move speed multiplier while rearing up: it keeps creeping in.")] float windUpMoveFactor = 0.2f;
        [SerializeField, Min(0.1f)] float slamRadius = 2.8f;
        [SerializeField, Min(0f)] float slamDamage = 40f;
        [SerializeField, Min(0f)] float recoverTime = 1f;
        [SerializeField, Min(0f)] float slamShake = 0.22f;
        [SerializeField, Tooltip("The shockwave, authored for the slam radius.")] GameObject slamEffect;

        float _timer;

        public State Current { get; private set; }
        public float SlamRadius => slamRadius;

        bool IAreaTelegraph.ShowsArea => Current == State.WindUp;
        float IAreaTelegraph.AreaRadius => slamRadius;
        float IAreaTelegraph.AreaArc => 360f;
        float IAreaTelegraph.AreaProgress => Mathf.Clamp01(1f - _timer / windUpTime);

        public override void ResetState(Enemy self)
        {
            Current = State.Approach;
            _timer = 0f;
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            var target = Target(self);
            Vector2 toTarget = target != null ? target.Position - self.Position : Vector2.zero;
            Vector2 chase = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized * self.MoveSpeed : Vector2.zero;

            switch (Current)
            {
                case State.Approach:
                    if (target != null && toTarget.magnitude <= triggerDistance)
                        Enter(self, State.WindUp, windUpTime);
                    return chase;

                case State.WindUp:
                    self.SpeedMultiplier = windUpMoveFactor;
                    _timer -= deltaTime * self.AttackRate;
                    if (_timer <= 0f)
                    {
                        Slam(self);
                        Enter(self, State.Recover, recoverTime);
                    }
                    return chase;

                default: // Recover
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 4f;
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                        Enter(self, State.Approach, 0f);
                    return Vector2.zero;
            }
        }

        void Slam(Enemy self)
        {
            bool nearby = false;
            foreach (var player in CombatRegistry.Players)
            {
                Vector2 away = player.Position - self.Position;
                nearby |= player.IsLocal && away.magnitude < slamRadius * 3f;
                if (!player.IsAlive || player.IsIntangible)
                    continue;
                if (away.magnitude > slamRadius + player.Radius)
                    continue;
                Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                player.ApplyDamage(new DamageInfo(slamDamage, self.Team, self.EntityId, self.Position + direction * slamRadius, direction, DamageKind.Contact));
            }

            if (slamEffect != null)
                Instantiate(slamEffect, PlaneMath.ToWorld(self.Position, 0.1f), Quaternion.identity);
            GameAudio.Play(SoundCue.Slam, transform.position);
            if (nearby)
                Presentation.CameraRig.Kick(slamShake); // felt up close, not from across the arena
        }

        void Enter(Enemy self, State state, float duration)
        {
            Current = state;
            _timer = duration;
            self.IsTelegraphing = state == State.WindUp;
        }
    }
}
