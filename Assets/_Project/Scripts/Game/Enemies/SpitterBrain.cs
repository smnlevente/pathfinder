using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Spitter (M7, galaxy 3): a squat Swarm pod that keeps its distance and lobs seed globs at where the ship is. A landing
    /// mark shows where each glob will come down, with a ring closing in on it; it hurts whoever is still under it when it
    /// lands, and bursts into a ring of thorns flying outward. Globs in the air when it dies fall harmlessly.
    /// </summary>
    public sealed class SpitterBrain : EnemyBrain
    {
        public enum State { Hold, WindUp }

        struct Glob
        {
            public Vector2 Target;
            public float Timer;
            public GameObject View;
            public Presentation.ImpactTelegraph Mark;
        }

        [Header("Positioning")]
        [SerializeField, Min(0f)] float preferredDistance = 10f;
        [SerializeField, Range(0f, 1f)] float strafeFactor = 0.4f;

        [Header("Lob")]
        [SerializeField, Min(0.5f)] float lobInterval = 3.4f;
        [SerializeField, Min(0f)] float windUpTime = 0.5f;
        [SerializeField, Min(0f)] float lobRange = 16f;
        [SerializeField, Min(0.1f), Tooltip("Flight time: the landing ring closes in over it.")] float flightTime = 1.1f;
        [SerializeField, Min(0.1f)] float globRadius = 1.6f;
        [SerializeField, Min(0f)] float globDamage = 12f;
        [SerializeField] GameObject markPrefab;
        [SerializeField, Tooltip("A one-shot flash where a glob lands.")] GameObject burstPrefab;

        [Header("Thorns")]
        [SerializeField, Min(0)] int thorns = 6;
        [SerializeField, Min(0.1f)] float thornSpeed = 5.5f;
        [SerializeField, Min(0f)] float thornDamage = 8f;
        [SerializeField, Min(0.1f)] float thornRange = 9f;
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] GameObject bulletImpactPrefab;

        readonly List<Glob> _globs = new List<Glob>();
        float _cooldown, _timer, _strafeSign;
        int _selfId;

        public State Current { get; private set; }

        /// <summary>What this enemy shoots (playtest 7: each shooter has its own kind of shot).</summary>
        public GameObject BulletPrefab => bulletPrefab;

        public float GlobRadius => globRadius;
        public int GlobsInFlight => _globs.Count;

        /// <summary>Where the newest glob will land, if one is in the air. For tests.</summary>
        public bool TryGetIncomingGlob(out Vector2 target, out float progress)
        {
            target = Vector2.zero;
            progress = 0f;
            if (_globs.Count == 0)
                return false;
            var glob = _globs[_globs.Count - 1];
            target = glob.Target;
            progress = 1f - glob.Timer / flightTime;
            return true;
        }

        public override void ResetState(Enemy self)
        {
            _selfId = self.EntityId;
            _cooldown = self.Random.Range(1f, lobInterval * 0.75f);
            _strafeSign = self.Random.Range(0, 2) == 0 ? -1f : 1f;
            Current = State.Hold;
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            float attackDelta = deltaTime * self.AttackRate;
            var target = Target(self);
            Vector2 desired = Vector2.zero;

            if (target != null)
            {
                Vector2 toTarget = target.Position - self.Position;
                float distance = toTarget.magnitude;
                Vector2 inward = distance > 1e-4f ? toTarget / distance : Vector2.up;
                self.FaceTowards(inward);

                if (Current == State.WindUp)
                {
                    self.SpeedMultiplier = 0.2f;
                    _timer -= attackDelta;
                    if (_timer <= 0f)
                    {
                        Lob(self, world, target);
                        Current = State.Hold;
                        self.IsTelegraphing = false;
                        _cooldown = lobInterval;
                    }
                }
                else
                {
                    // Keep its distance, drifting sideways a little.
                    float pull = Mathf.Clamp((distance - preferredDistance) * 0.4f, -1f, 1f);
                    Vector2 side = new Vector2(-inward.y, inward.x) * _strafeSign;
                    desired = (inward * pull + side * strafeFactor) * self.MoveSpeed;
                    _cooldown -= attackDelta;
                    if (_cooldown <= 0f && distance <= lobRange)
                    {
                        Current = State.WindUp;
                        _timer = windUpTime;
                        self.IsTelegraphing = true;
                    }
                }
            }

            UpdateGlobs(self, world, deltaTime);
            return desired;
        }

        void Lob(Enemy self, CombatWorld world, PlayerShip target)
        {
            var glob = new Glob { Target = world.ClampToArena(target.Position, globRadius * 0.5f), Timer = flightTime };
            if (markPrefab != null)
            {
                glob.View = Instantiate(markPrefab, PlaneMath.ToWorld(glob.Target), Quaternion.identity);
                glob.Mark = glob.View.GetComponent<Presentation.ImpactTelegraph>();
                glob.Mark?.Launch(self.Position, glob.Target, globRadius);
            }
            _globs.Add(glob);
            _strafeSign = -_strafeSign;
            GameAudio.Play(SoundCue.SporePod, transform.position);
        }

        void UpdateGlobs(Enemy self, CombatWorld world, float deltaTime)
        {
            for (int i = _globs.Count - 1; i >= 0; i--)
            {
                var glob = _globs[i];
                glob.Timer -= deltaTime;
                if (glob.Timer > 0f)
                {
                    glob.Mark?.Show(glob.Target, 1f - glob.Timer / flightTime, glob.Timer < 0.25f);
                    _globs[i] = glob;
                    continue;
                }
                if (glob.View != null)
                    Destroy(glob.View);
                _globs.RemoveAt(i);
                Land(self, world, glob.Target);
            }
        }

        /// <summary>The glob hits whoever is under it, and bursts into a ring of thorns.</summary>
        void Land(Enemy self, CombatWorld world, Vector2 at)
        {
            GameAudio.Play(SoundCue.Erupt, PlaneMath.ToWorld(at));
            if (burstPrefab != null)
            {
                var burst = Instantiate(burstPrefab, PlaneMath.ToWorld(at), Quaternion.identity);
                if (burst.TryGetComponent(out Presentation.RingPulse pulse))
                    pulse.SizeMultiplier = globRadius;
            }
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive)
                    continue;
                Vector2 away = player.Position - at;
                float reach = globRadius + player.Radius;
                if (away.sqrMagnitude > reach * reach)
                    continue;
                player.ApplyDamage(new DamageInfo(globDamage, Team.Enemy, _selfId, player.Position,
                    away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up, DamageKind.Contact));
            }

            float offset = self.Random.Range(0f, 360f);
            for (int i = 0; i < thorns; i++)
            {
                Vector2 direction = PlaneMath.FromHeading(offset + i * 360f / thorns);
                world.Projectiles.Spawn(new ProjectileSpawn(at + direction * globRadius, direction, thornSpeed, thornDamage, 0.24f, thornRange,
                    self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));
            }
        }

        void ClearGlobs()
        {
            foreach (var glob in _globs)
            {
                if (glob.View != null)
                    Destroy(glob.View);
            }
            _globs.Clear();
        }

        void OnDestroy() => ClearGlobs();
    }
}
