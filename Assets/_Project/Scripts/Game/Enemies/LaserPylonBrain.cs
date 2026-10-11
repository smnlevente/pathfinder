using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// The Graveyard Drift's hazard (M7): a dead station's defence pylon, still sweeping a two-armed laser around itself.
    /// It powers down for a moment now and then; before it powers up again, a thin flickering line shows where the beam will
    /// be. Touching the live beam hurts. Shoot the pylon to switch it off for good. It never moves.
    /// </summary>
    public sealed class LaserPylonBrain : EnemyBrain
    {
        [SerializeField, Min(0.5f), Tooltip("Length of each arm of the beam.")] float reach = 7f;
        [SerializeField] float turnRate = 32f;
        [SerializeField, Min(0.5f)] float activeTime = 6f;
        [SerializeField, Min(0f)] float offTime = 1.2f;
        [SerializeField, Min(0f), Tooltip("Flickering warning before the beam comes back on.")] float warnTime = 1f;
        [SerializeField, Min(0f)] float beamDamage = 12f;
        [SerializeField, Min(0.05f)] float beamHalfWidth = 0.25f;
        [SerializeField] LineRenderer beam;
        [SerializeField] float beamHeight = 0.45f;

        enum State { Active, Off, Warning }

        State _state;
        float _timer, _angle;

        public bool BeamLive => _state == State.Active;

        /// <summary>The two ends of the beam (both arms), for tests.</summary>
        public (Vector2 from, Vector2 to) BeamEnds(Enemy self)
        {
            Vector2 axis = PlaneMath.FromHeading(_angle);
            return (self.Position - axis * reach, self.Position + axis * reach);
        }

        public override void ResetState(Enemy self)
        {
            _angle = self.Random.Range(0f, 360f);
            // Start dark with a warning, so it never switches on across a ship that just arrived.
            _state = State.Warning;
            _timer = warnTime + 0.5f;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            self.Armoured = false;
            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _state = _state switch { State.Active => State.Off, State.Off => State.Warning, _ => State.Active };
                _timer = _state switch { State.Active => activeTime, State.Off => offTime, _ => warnTime };
            }
            self.IsTelegraphing = _state == State.Warning;
            _angle += turnRate * deltaTime;

            var (from, to) = BeamEnds(self);
            if (_state == State.Active)
                HurtPlayersOn(self, from, to);
            ShowBeam(from, to);
            return Vector2.zero;
        }

        void HurtPlayersOn(Enemy self, Vector2 from, Vector2 to)
        {
            Vector2 axis = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive || player.IsIntangible || !PlaneMath.SegmentHitsCircle(from, to, player.Position, player.Radius + beamHalfWidth, out _))
                    continue;
                Vector2 closest = from + axis * Mathf.Clamp(Vector2.Dot(player.Position - from, axis), 0f, length);
                Vector2 away = player.Position - closest;
                Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : new Vector2(-axis.y, axis.x);
                player.ApplyDamage(new DamageInfo(beamDamage, self.Team, self.EntityId, closest, direction, DamageKind.Contact));
            }
        }

        void ShowBeam(Vector2 from, Vector2 to)
        {
            if (beam == null)
                return;
            beam.enabled = _state != State.Off && (_state == State.Active || Mathf.Repeat(Time.time * 12f, 1f) < 0.5f);
            beam.widthMultiplier = _state == State.Active ? beamHalfWidth * 2f : beamHalfWidth * 0.5f;
            beam.SetPosition(0, PlaneMath.ToWorld(from, beamHeight));
            beam.SetPosition(1, PlaneMath.ToWorld(to, beamHeight));
        }

        void OnDisable()
        {
            if (beam != null)
                beam.enabled = false;
        }
    }
}
