using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// The Quarry Maw, guardian of the Shattered Expanse (GDD §7.5): a colossal rock-worm with three phases.
    /// <list type="number">
    /// <item><b>Burrow</b> (above 66%): it dives underground. A red glow marks where it will erupt; the eruption hits hard
    /// and throws out a ring of rock bullets. It then chases on the surface before diving again.</item>
    /// <item><b>Sweep</b> (66–33%): its segmented body charges across the arena from edge to edge. Yellow weak-point
    /// segments take double damage, and armoured ones half.</item>
    /// <item><b>Enrage</b> (below 33%): it circles the ship and spews aimed bullet streams from its maw, with rock showers.</item>
    /// </list>
    /// Each phase change starts with a roar, during which it can't be hurt.
    /// </summary>
    public sealed class QuarryMawBrain : EnemyBrain, IDamageModifier
    {
        public enum Phase { Burrow, Sweep, Enrage }

        enum Action { Surfaced, Underground, Roar, SweepWindUp, Sweeping, SweepPause, Orbiting }

        [Header("Body")]
        [SerializeField] BossSegment segmentPrefab;
        [SerializeField, Min(1)] int segmentCount = 10;
        [SerializeField, Min(0.2f)] float segmentSpacing = 1.1f;
        [SerializeField, Min(2)] int weakPointEvery = 3;

        [Header("Phase 1: Burrow")]
        [SerializeField, Min(0.1f)] float surfacedTime = 3.5f;
        [SerializeField, Min(0.1f)] float undergroundTime = 1.8f;
        [SerializeField, Min(0.1f), Tooltip("The glow tracks the ship for this long, then locks.")] float trackTime = 1.1f;
        [SerializeField, Min(0.1f)] float eruptRadius = 3f;
        [SerializeField, Min(0f)] float eruptDamage = 30f;
        [SerializeField, Min(3)] int rockCount = 14;
        [SerializeField, Min(0.1f)] float rockSpeed = 6f;
        [SerializeField] GameObject eruptTelegraphPrefab;
        [SerializeField] GameObject eruptEffect;

        [Header("Phase 2: Sweep")]
        [SerializeField, Min(0.1f)] float sweepSpeed = 13f;
        [SerializeField, Min(0f)] float sweepWindUp = 1f;
        [SerializeField, Min(0f)] float sweepPause = 1.4f;
        [SerializeField, Min(0f)] float sweepAmplitude = 5f;

        [Header("Phase 3: Enrage")]
        [SerializeField, Min(1f)] float orbitDistance = 9f;
        [SerializeField, Min(0.1f)] float streamInterval = 0.55f;
        [SerializeField, Min(1)] int streamBullets = 5;
        [SerializeField, Min(0f)] float streamSpread = 36f;
        [SerializeField, Min(1f)] float rockShowerInterval = 6f;

        [Header("Bullets")]
        [SerializeField, Min(0f)] float bulletDamage = 10f;
        [SerializeField, Min(0.1f)] float bulletSpeed = 9f;
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] GameObject bulletImpactPrefab;

        [Header("Roar")]
        [SerializeField, Min(0f)] float roarTime = 1.5f;

        readonly List<BossSegment> _segments = new List<BossSegment>();
        readonly List<Vector2> _trail = new List<Vector2>();
        Action _action;
        float _timer;
        float _attackTimer;
        float _showerTimer;
        Vector2 _eruptTarget;
        GameObject _telegraph;
        Vector2 _sweepFrom, _sweepTo;
        int _sweepSide = 1;
        float _orbitSign = 1f;
        bool _spawned;

        public Phase CurrentPhase { get; private set; }
        public bool IsBurrowed => _action == Action.Underground;
        public bool IsRoaring => _action == Action.Roar;
        public IReadOnlyList<BossSegment> Segments => _segments;

        public override void ResetState(Enemy self)
        {
            CurrentPhase = Phase.Burrow;
            _action = Action.Surfaced;
            _timer = surfacedTime * 0.6f;
            _orbitSign = self.Random.Chance(0.5f) ? 1f : -1f;
            self.Burrowed = false;
            self.IsTelegraphing = false;
        }

        public float IncomingDamageMultiplier(Enemy self, in DamageInfo info) => IsBurrowed || IsRoaring ? 0f : 1f;

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            if (!_spawned)
                SpawnBody(self);

            var target = Target(self);
            Vector2 desired = Vector2.zero;

            var phase = PhaseFor(self.Vitals.Hull / self.Vitals.MaxHull);
            if (phase != CurrentPhase && _action != Action.Underground)
            {
                CurrentPhase = phase;
                Enter(self, Action.Roar, roarTime);
            }

            _timer -= deltaTime;
            switch (_action)
            {
                case Action.Roar:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 6f;
                    if (_timer <= 0f)
                        StartPhase(self, world, target);
                    break;

                case Action.Surfaced:
                    if (target != null)
                        desired = (target.Position - self.Position).normalized * self.MoveSpeed;
                    if (_timer <= 0f)
                        Dive(self, target);
                    break;

                case Action.Underground:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 20f;
                    if (target != null && _timer > undergroundTime - trackTime)
                        _eruptTarget = world.ClampToArena(target.Position, 2f);
                    if (_telegraph != null)
                        _telegraph.transform.position = PlaneMath.ToWorld(_eruptTarget);
                    if (_timer <= 0f)
                        Erupt(self, world);
                    break;

                case Action.SweepWindUp:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 20f;
                    self.FaceTowards(_sweepTo - _sweepFrom);
                    if (_timer <= 0f)
                        Enter(self, Action.Sweeping, Vector2.Distance(_sweepFrom, _sweepTo) / sweepSpeed + 1f);
                    break;

                case Action.Sweeping:
                {
                    // Charge along the edge-to-edge line with a sinuous weave.
                    self.SpeedMultiplier = sweepSpeed / Mathf.Max(self.MoveSpeed, 0.01f);
                    self.AccelerationMultiplier = 20f;
                    Vector2 axis = (_sweepTo - _sweepFrom).normalized;
                    float along = Vector2.Dot(self.Position - _sweepFrom, axis);
                    Vector2 side = new Vector2(-axis.y, axis.x);
                    Vector2 aim = _sweepFrom + axis * (along + 3f) + side * (Mathf.Sin(along * 0.35f) * sweepAmplitude);
                    desired = (aim - self.Position).normalized * sweepSpeed;
                    if (along >= Vector2.Distance(_sweepFrom, _sweepTo) - 1f || _timer <= 0f)
                        Enter(self, Action.SweepPause, sweepPause);
                    break;
                }

                case Action.SweepPause:
                    self.SpeedMultiplier = 0.3f;
                    if (_timer <= 0f)
                        PrepareSweep(self, world);
                    break;

                case Action.Orbiting:
                    if (target != null)
                    {
                        Vector2 toTarget = target.Position - self.Position;
                        float distance = toTarget.magnitude;
                        Vector2 forward = distance > 1e-4f ? toTarget / distance : Vector2.up;
                        Vector2 tangent = new Vector2(-forward.y, forward.x) * _orbitSign;
                        float radial = Mathf.Clamp((distance - orbitDistance) * 0.4f, -1f, 1f);
                        desired = (tangent + forward * radial).normalized * self.MoveSpeed * 1.2f;
                        self.FaceTowards(forward);

                        _attackTimer -= deltaTime * self.AttackRate;
                        if (_attackTimer <= 0f)
                        {
                            FireStream(self, world, forward);
                            _attackTimer = streamInterval;
                        }
                        _showerTimer -= deltaTime;
                        if (_showerTimer <= 0f)
                        {
                            FireRing(self, world, self.Position, rockCount);
                            _showerTimer = rockShowerInterval;
                        }
                    }
                    break;
            }

            UpdateBody(self);
            return desired;
        }

        static Phase PhaseFor(float healthFraction) =>
            healthFraction > 2f / 3f ? Phase.Burrow : healthFraction > 1f / 3f ? Phase.Sweep : Phase.Enrage;

        void StartPhase(Enemy self, CombatWorld world, PlayerShip target)
        {
            switch (CurrentPhase)
            {
                case Phase.Burrow:
                    Enter(self, Action.Surfaced, surfacedTime);
                    break;
                case Phase.Sweep:
                    PrepareSweep(self, world);
                    break;
                default:
                    _attackTimer = streamInterval;
                    _showerTimer = rockShowerInterval * 0.5f;
                    Enter(self, Action.Orbiting, float.MaxValue);
                    break;
            }
        }

        void Dive(Enemy self, PlayerShip target)
        {
            _eruptTarget = target != null ? target.Position : self.Position;
            if (eruptTelegraphPrefab != null)
                _telegraph = Instantiate(eruptTelegraphPrefab, PlaneMath.ToWorld(_eruptTarget), Quaternion.identity);
            Enter(self, Action.Underground, undergroundTime);
        }

        void Erupt(Enemy self, CombatWorld world)
        {
            if (_telegraph != null)
                Destroy(_telegraph);

            // Burst out of the ground at the marked spot, with the body trailing up behind.
            self.Teleport(_eruptTarget);
            ResetTrail(self.Position);

            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive && !player.IsIntangible && Vector2.Distance(player.Position, _eruptTarget) <= eruptRadius + player.Radius)
                {
                    Vector2 away = (player.Position - _eruptTarget).sqrMagnitude > 1e-6f ? (player.Position - _eruptTarget).normalized : Vector2.up;
                    player.ApplyDamage(new DamageInfo(eruptDamage, self.Team, self.EntityId, _eruptTarget, away, DamageKind.Contact));
                }
            }
            FireRing(self, world, _eruptTarget, rockCount);
            if (eruptEffect != null)
                Instantiate(eruptEffect, PlaneMath.ToWorld(_eruptTarget, 0.5f), Quaternion.identity);

            Enter(self, Action.Surfaced, surfacedTime);
        }

        void PrepareSweep(Enemy self, CombatWorld world)
        {
            // Alternate sides. Start just inside one arena edge and charge to the opposite one.
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(3f, 3f);
            float lane = self.Random.Range(-half.y * 0.6f, half.y * 0.6f);
            _sweepFrom = new Vector2(-half.x * _sweepSide, lane);
            _sweepTo = new Vector2(half.x * _sweepSide, -lane);
            _sweepSide = -_sweepSide;

            self.Teleport(_sweepFrom);
            ResetTrail(_sweepFrom);
            Enter(self, Action.SweepWindUp, sweepWindUp);
        }

        void FireStream(Enemy self, CombatWorld world, Vector2 forward)
        {
            Vector2 mouth = self.Position + forward * (self.Radius + 0.3f);
            for (int i = 0; i < streamBullets; i++)
            {
                float t = streamBullets > 1 ? i / (streamBullets - 1f) - 0.5f : 0f;
                Vector2 direction = PlaneMath.Rotate(forward, t * streamSpread);
                world.Projectiles.Spawn(new ProjectileSpawn(mouth, direction, bulletSpeed, bulletDamage, 0.28f, 26f,
                    self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));
            }
        }

        void FireRing(Enemy self, CombatWorld world, Vector2 center, int count)
        {
            float offset = self.Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = PlaneMath.FromHeading(offset + i * 360f / count);
                world.Projectiles.Spawn(new ProjectileSpawn(center + direction * 1.2f, direction, rockSpeed, bulletDamage, 0.28f, 22f,
                    self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));
            }
        }

        void Enter(Enemy self, Action action, float duration)
        {
            _action = action;
            _timer = duration;
            self.Burrowed = action == Action.Underground;
            self.IsTelegraphing = action == Action.Roar || action == Action.SweepWindUp;
        }

        // ---------------------------------------------------------------- body

        void SpawnBody(Enemy self)
        {
            _spawned = true;
            ResetTrail(self.Position);
            self.Died += (entity, info) => ShatterBody();
            if (segmentPrefab == null)
                return;

            for (int i = 0; i < segmentCount; i++)
            {
                var segment = Instantiate(segmentPrefab, transform.position, Quaternion.identity);
                float taper = Mathf.Lerp(1f, 0.6f, i / Mathf.Max(1f, segmentCount - 1f));
                segment.transform.localScale = Vector3.one * taper;
                segment.Attach(self, weakPoint: (i + 1) % weakPointEvery == 0);
                _segments.Add(segment);
            }
        }

        void ResetTrail(Vector2 position)
        {
            _trail.Clear();
            _trail.Add(position);
        }

        /// <summary>Records the head's path and places each segment a fixed distance behind it along that path.</summary>
        void UpdateBody(Enemy self)
        {
            Vector2 head = self.Position;
            if (_trail.Count == 0 || (_trail[0] - head).sqrMagnitude > 0.01f)
                _trail.Insert(0, head);

            float needed = (segmentCount + 1) * segmentSpacing;
            float length = 0f;
            for (int i = 1; i < _trail.Count; i++)
            {
                length += Vector2.Distance(_trail[i - 1], _trail[i]);
                if (length > needed)
                {
                    _trail.RemoveRange(i + 1, _trail.Count - i - 1);
                    break;
                }
            }

            for (int s = 0; s < _segments.Count; s++)
            {
                if (_segments[s] == null)
                    continue;
                Vector2 position = PointAlongTrail((s + 1) * segmentSpacing, out Vector2 facing);
                _segments[s].Follow(position, facing);
            }
        }

        Vector2 PointAlongTrail(float distance, out Vector2 facing)
        {
            facing = Vector2.zero;
            float travelled = 0f;
            for (int i = 1; i < _trail.Count; i++)
            {
                Vector2 from = _trail[i - 1], to = _trail[i];
                float step = Vector2.Distance(from, to);
                if (travelled + step >= distance && step > 1e-5f)
                {
                    facing = from - to;
                    return Vector2.Lerp(from, to, (distance - travelled) / step);
                }
                travelled += step;
            }
            // While the body is still emerging, segments bunch up at the end of the recorded path.
            return _trail[_trail.Count - 1];
        }

        /// <summary>The body bursts apart when the boss dies.</summary>
        void ShatterBody()
        {
            if (_telegraph != null)
                Destroy(_telegraph);
            foreach (var segment in _segments)
            {
                if (segment != null)
                    segment.Shatter();
            }
            _segments.Clear();
        }

        void OnDestroy()
        {
            // Level teardown or scene unload: remove the body quietly. Spawning effects here would leak objects.
            if (_telegraph != null)
                Destroy(_telegraph);
            foreach (var segment in _segments)
            {
                if (segment != null)
                    Destroy(segment.gameObject);
            }
            _segments.Clear();
        }
    }
}
