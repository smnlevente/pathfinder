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
    /// The Quarry Maw, guardian of the Shattered Expanse (GDD §7.5): a colossal rock-worm with three phases. Its shell
    /// is armour: shots only hurt the glowing yellow weak points along its body, and its maw whenever it opens.
    /// <list type="number">
    /// <item><b>Burrow</b> (above 66%): it slithers around the ship side-on, spitting aimed bursts, so its weak points
    /// face the guns. Then it rears up, opens its maw and sinks into the ground, a window to shoot it. A red mark tracks
    /// the ship: an outline at the blast radius, a fill growing out to it and a ring closing in to meet it as a timer.
    /// The spot locks only 0.4 s before impact, too late to walk clear, so it has to be dashed (playtest 6, 7). It bursts out with two rings of rocks
    /// and rises, maw open again.</item>
    /// <item><b>Sweep</b> (66–33%): volleys of two charges from wherever it is, through the ship's position, each shown by
    /// a chevron lane first. Charging, it sheds walls of rocks to both sides. After a volley it rests, maw open, coiling
    /// slowly around the ship: the big window to hurt it.</item>
    /// <item><b>Enrage</b> (below 33%): its maw stays open. It circles the ship spewing bullet streams, with rock showers
    /// and spinning spiral barrages.</item>
    /// </list>
    /// Each phase change starts with a roar, during which it is fully armoured.
    /// </summary>
    public sealed class QuarryMawBrain : EnemyBrain, IDamageModifier, IChargeTelegraph, ISinkDepth
    {
        public enum Phase { Burrow, Sweep, Enrage }

        enum Action { Surfaced, Diving, Underground, Emerging, Roar, SweepWindUp, Sweeping, SweepRest, Orbiting, SpiralWindUp, Spiral }

        [Header("Body")]
        [SerializeField] BossSegment segmentPrefab;
        [SerializeField, Min(1)] int segmentCount = 12;
        [SerializeField, Min(0.2f)] float segmentSpacing = 1.1f;
        [SerializeField, Min(2)] int weakPointEvery = 3;
        [SerializeField, Tooltip("Shown on the head while its maw is open and can be shot.")] GameObject exposedMawMarker;

        [Header("Phase 1: Burrow")]
        [SerializeField, Min(0.1f)] float surfacedTime = 5f;
        [SerializeField, Min(1f), Tooltip("It slithers around the ship at about this distance, side-on.")] float slitherDistance = 8f;
        [SerializeField, Min(0.1f), Tooltip("Rearing up and sinking: maw open, can be shot.")] float diveTime = 1.4f;
        [SerializeField, Min(0.1f), Tooltip("Rising after the eruption: maw open, can be shot.")] float emergeTime = 1.2f;
        [SerializeField, Min(0.1f)] float undergroundTime = 2f;
        [SerializeField, Min(0.05f), Tooltip("The mark follows the ship until this long before the eruption, then locks.")] float lockTime = 0.4f;
        [SerializeField, Min(0.1f), Tooltip("Playtest 7: 4 u felt too big; with the 0.4 s lock, walking still can't clear 3 u, a dash can.")]
        float eruptRadius = 3f;
        [SerializeField, Min(0f)] float eruptDamage = 30f;
        [SerializeField, Min(3)] int rockCount = 16;
        [SerializeField, Min(0.1f)] float rockSpeed = 6f;
        [SerializeField, Min(0.1f), Tooltip("The eruption's second, slower ring.")] float slowRockSpeed = 3.8f;
        [SerializeField, Min(0.1f)] float surfacedBurstInterval = 1.6f;
        [SerializeField] GameObject eruptTelegraphPrefab;
        [SerializeField] GameObject eruptEffect;
        [SerializeField, Tooltip("A sharp ring snapping out over the blast radius when it erupts.")] GameObject eruptBurstPrefab;
        [SerializeField, Tooltip("Kicked up as it digs in and out.")] GameObject burrowDustPrefab;

        [Header("Phase 2: Sweep")]
        [SerializeField, Min(0.1f)] float sweepSpeed = 13f;
        [SerializeField, Min(0f)] float firstSweepWindUp = 1.4f;
        [SerializeField, Min(0f)] float chainedSweepWindUp = 1f;
        [SerializeField, Min(1)] int sweepsPerVolley = 2;
        [SerializeField, Min(0f)] float sweepRestTime = 4.5f;
        [SerializeField, Min(0.02f), Tooltip("Seconds between the rock pairs shed while charging.")] float wallInterval = 0.18f;
        [SerializeField, Min(0.1f)] float wallRockSpeed = 3.5f;
        [SerializeField, Min(1f)] float sweepContactMultiplier = 1.6f;

        [Header("Phase 3: Enrage")]
        [SerializeField, Min(1f)] float orbitDistance = 9f;
        [SerializeField, Min(1f), Tooltip("Enraged, it circles this much faster than its normal speed (playtest 5).")] float enrageSpeedMultiplier = 1.6f;
        [SerializeField, Min(0.1f)] float streamInterval = 0.6f;
        [SerializeField, Min(1)] int streamBullets = 5;
        [SerializeField, Min(0f)] float streamSpread = 36f;
        [SerializeField, Min(1f)] float rockShowerInterval = 5f;
        [SerializeField, Min(1f)] float spiralInterval = 8f;
        [SerializeField, Min(0f)] float spiralWindUp = 0.6f;
        [SerializeField, Min(0.1f)] float spiralDuration = 2.4f;
        [SerializeField, Min(1)] int spiralArms = 3;
        [SerializeField] float spiralTurnRate = 140f;
        [SerializeField, Min(0.02f)] float spiralEmitInterval = 0.1f;
        [SerializeField, Min(0.1f)] float spiralBulletSpeed = 6f;

        [Header("Bullets")]
        [SerializeField, Min(0f)] float bulletDamage = 10f;
        [SerializeField, Min(0.1f)] float bulletSpeed = 9f;
        [SerializeField] GameObject bulletPrefab;

        /// <summary>What this enemy shoots (playtest 7: each shooter has its own kind of shot).</summary>
        public GameObject BulletPrefab => bulletPrefab;
        [SerializeField] GameObject bulletImpactPrefab;

        [Header("Roar")]
        [SerializeField, Min(0f)] float roarTime = 1.5f;

        readonly List<BossSegment> _segments = new List<BossSegment>();
        readonly List<Vector2> _trail = new List<Vector2>();
        Action _action;
        float _timer;
        float _actionDuration;
        float _attackTimer;
        float _showerTimer;
        float _spiralTimer;
        float _spiralAngle;
        Vector2 _eruptTarget;
        GameObject _telegraph;
        Presentation.ImpactTelegraph _telegraphView;
        Vector2 _sweepFrom, _sweepTo;
        int _sweepsLeft;
        float _orbitSign = 1f;
        bool _spawned;

        public Phase CurrentPhase { get; private set; }
        public bool IsBurrowed => _action == Action.Underground;

        /// <summary>Where it will burst out, while burrowed.</summary>
        public Vector2 EruptTarget => _eruptTarget;
        public float EruptRadius => eruptRadius;

        /// <summary>Burrowed and no longer following the ship: the eruption is less than <c>lockTime</c> away.</summary>
        public bool EruptLocked => IsBurrowed && _timer <= lockTime;
        public bool IsRoaring => _action == Action.Roar;

        /// <summary>
        /// The maw is open and the head can be hit: while it dives and emerges, while it rests between sweeps, and all
        /// through the enrage, but never during a roar.
        /// </summary>
        public bool IsMawExposed => !IsRoaring && (CurrentPhase == Phase.Enrage
            || _action == Action.Diving || _action == Action.Emerging || _action == Action.SweepRest);

        /// <summary>0 on the surface, 1 underground; in between while diving or emerging.</summary>
        public float SinkDepth => _action switch
        {
            Action.Diving => Progress,
            Action.Underground => 1f,
            Action.Emerging => 1f - Progress,
            _ => 0f,
        };

        float Progress => _actionDuration > 0f ? Mathf.Clamp01(1f - _timer / _actionDuration) : 1f;

        public IReadOnlyList<BossSegment> Segments => _segments;

        bool IChargeTelegraph.ShowsCharge => _action == Action.SweepWindUp;
        Vector2 IChargeTelegraph.ChargeStart => _sweepFrom;
        Vector2 IChargeTelegraph.ChargeDirection => (_sweepTo - _sweepFrom).normalized;
        float IChargeTelegraph.ChargeLength => Vector2.Distance(_sweepFrom, _sweepTo);
        float IChargeTelegraph.ChargeProgress => _actionDuration > 0f ? Mathf.Clamp01(1f - _timer / _actionDuration) : 1f;

        public override void ResetState(Enemy self)
        {
            CurrentPhase = Phase.Burrow;
            _orbitSign = self.Random.Chance(0.5f) ? 1f : -1f;
            Enter(self, Action.Surfaced, surfacedTime * 0.6f);
            _attackTimer = surfacedBurstInterval;
        }

        public float IncomingDamageMultiplier(Enemy self, in DamageInfo info) => IsBurrowed || IsRoaring ? 0f : 1f;

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            if (!_spawned)
                SpawnBody(self);

            var target = Target(self);
            Vector2 desired = Vector2.zero;

            // A phase change waits until it is back on the surface, so a dive always plays out.
            var phase = PhaseFor(self.Vitals.Hull / self.Vitals.MaxHull);
            bool digging = _action == Action.Diving || _action == Action.Underground || _action == Action.Emerging;
            if (phase != CurrentPhase && !digging)
            {
                CurrentPhase = phase;
                Enter(self, Action.Roar, roarTime);
                GameAudio.Play(SoundCue.Roar, transform.position);
                Presentation.CameraRig.Kick(0.25f);
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
                    {
                        // Slither around the ship, side-on, so the weak points on its flank face the guns.
                        desired = Circle(self, target, slitherDistance, 1f);
                        if (Tick(ref _attackTimer, deltaTime * self.AttackRate, surfacedBurstInterval))
                            FireFan(self, world, (target.Position - self.Position).normalized, 3, 20f, bulletSpeed);
                    }
                    if (_timer <= 0f)
                        Enter(self, Action.Diving, diveTime, dust: true);
                    break;

                case Action.Diving:
                    // Rear up, maw open, and sink: a window to shoot it.
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 4f;
                    if (target != null)
                        self.FaceTowards(target.Position - self.Position);
                    if (_timer <= 0f)
                        Dive(self, target);
                    break;

                case Action.Emerging:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 10f;
                    if (target != null)
                        self.FaceTowards(target.Position - self.Position);
                    if (_timer <= 0f)
                    {
                        _attackTimer = surfacedBurstInterval;
                        Enter(self, Action.Surfaced, surfacedTime);
                    }
                    break;

                case Action.Underground:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 20f;
                    if (target != null && _timer > lockTime)
                        _eruptTarget = world.ClampToArena(target.Position, 2f);
                    if (_telegraphView != null)
                        _telegraphView.Show(_eruptTarget, 1f - _timer / undergroundTime, _timer <= lockTime);
                    if (_timer <= 0f)
                        Erupt(self, world);
                    break;

                case Action.SweepWindUp:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 20f;
                    self.FaceTowards(_sweepTo - _sweepFrom);
                    if (_timer <= 0f)
                    {
                        _attackTimer = 0f;
                        Enter(self, Action.Sweeping, Vector2.Distance(_sweepFrom, _sweepTo) / sweepSpeed + 0.5f);
                    }
                    break;

                case Action.Sweeping:
                {
                    // A straight charge, exactly along the telegraphed lane, shedding a rock to each side as it goes.
                    Vector2 axis = (_sweepTo - _sweepFrom).normalized;
                    self.SpeedMultiplier = sweepSpeed / Mathf.Max(self.MoveSpeed, 0.01f);
                    self.AccelerationMultiplier = 30f;
                    self.ContactDamageMultiplier = sweepContactMultiplier;
                    self.FaceTowards(axis);
                    desired = axis * sweepSpeed;

                    if (Tick(ref _attackTimer, deltaTime, wallInterval))
                    {
                        Vector2 side = new Vector2(-axis.y, axis.x);
                        SpawnBullet(self, world, self.Position + side * 0.8f, side, wallRockSpeed, 30f);
                        SpawnBullet(self, world, self.Position - side * 0.8f, -side, wallRockSpeed, 30f);
                    }

                    float along = Vector2.Dot(self.Position - _sweepFrom, axis);
                    if (along >= Vector2.Distance(_sweepFrom, _sweepTo) - 0.5f || _timer <= 0f)
                    {
                        if (--_sweepsLeft > 0 && target != null)
                            PrepareSweep(self, world, target, chainedSweepWindUp);
                        else
                        {
                            _attackTimer = surfacedBurstInterval * 0.5f;
                            Enter(self, Action.SweepRest, sweepRestTime);
                        }
                    }
                    break;
                }

                case Action.SweepRest:
                    // Tired, maw open, coiling slowly around the ship side-on: the big window to hurt it.
                    if (target != null)
                    {
                        desired = Circle(self, target, slitherDistance, 0.6f);
                        if (Tick(ref _attackTimer, deltaTime * self.AttackRate, surfacedBurstInterval * 1.5f))
                            FireFan(self, world, (target.Position - self.Position).normalized, 3, 20f, bulletSpeed);
                    }
                    if (_timer <= 0f && target != null)
                        StartVolley(self, world, target);
                    break;

                case Action.Orbiting:
                    if (target != null)
                    {
                        Vector2 toTarget = target.Position - self.Position;
                        float distance = toTarget.magnitude;
                        Vector2 forward = distance > 1e-4f ? toTarget / distance : Vector2.up;
                        Vector2 tangent = new Vector2(-forward.y, forward.x) * _orbitSign;
                        float radial = Mathf.Clamp((distance - orbitDistance) * 0.4f, -1f, 1f);
                        self.SpeedMultiplier = enrageSpeedMultiplier;
                        desired = (tangent + forward * radial).normalized * self.MoveSpeed * enrageSpeedMultiplier;
                        self.FaceTowards(forward);

                        if (Tick(ref _attackTimer, deltaTime * self.AttackRate, streamInterval))
                            FireFan(self, world, forward, streamBullets, streamSpread, bulletSpeed);
                        if (Tick(ref _showerTimer, deltaTime, rockShowerInterval))
                            FireRing(self, world, self.Position, rockCount, rockSpeed);
                    }
                    if (Tick(ref _spiralTimer, deltaTime, spiralInterval))
                        Enter(self, Action.SpiralWindUp, spiralWindUp);
                    break;

                case Action.SpiralWindUp:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 10f;
                    if (_timer <= 0f)
                    {
                        _spiralAngle = self.Random.Range(0f, 360f);
                        _attackTimer = 0f;
                        Enter(self, Action.Spiral, spiralDuration);
                    }
                    break;

                case Action.Spiral:
                    self.SpeedMultiplier = 0f;
                    _spiralAngle += spiralTurnRate * deltaTime * _orbitSign;
                    if (Tick(ref _attackTimer, deltaTime, spiralEmitInterval))
                    {
                        for (int arm = 0; arm < spiralArms; arm++)
                        {
                            Vector2 direction = PlaneMath.FromHeading(_spiralAngle + arm * 360f / spiralArms);
                            SpawnBullet(self, world, self.Position + direction * (self.Radius + 0.3f), direction, spiralBulletSpeed, 26f);
                        }
                    }
                    if (_timer <= 0f)
                    {
                        _attackTimer = streamInterval;
                        Enter(self, Action.Orbiting, float.MaxValue);
                    }
                    break;
            }

            UpdateArmour(self);
            UpdateBody(self);
            return desired;
        }

        /// <summary>Moves around the target at about <paramref name="distance"/>, mostly sideways, facing along its path.</summary>
        Vector2 Circle(Enemy self, PlayerShip target, float distance, float speedFactor)
        {
            Vector2 toTarget = target.Position - self.Position;
            float range = toTarget.magnitude;
            Vector2 forward = range > 1e-4f ? toTarget / range : Vector2.up;
            Vector2 tangent = new Vector2(-forward.y, forward.x) * _orbitSign;
            float radial = Mathf.Clamp((range - distance) * 0.4f, -1f, 1f);
            Vector2 direction = (tangent + forward * radial).normalized;
            self.FaceTowards(direction);
            return direction * self.MoveSpeed * speedFactor;
        }

        static Phase PhaseFor(float healthFraction) =>
            healthFraction > 2f / 3f ? Phase.Burrow : healthFraction > 1f / 3f ? Phase.Sweep : Phase.Enrage;

        /// <summary>Counts a timer down and, when it runs out, rearms it and returns true.</summary>
        static bool Tick(ref float timer, float deltaTime, float interval)
        {
            timer -= deltaTime;
            if (timer > 0f)
                return false;
            timer += interval;
            return true;
        }

        void StartPhase(Enemy self, CombatWorld world, PlayerShip target)
        {
            switch (CurrentPhase)
            {
                case Phase.Burrow:
                    _attackTimer = surfacedBurstInterval;
                    Enter(self, Action.Surfaced, surfacedTime);
                    break;
                case Phase.Sweep:
                    if (target != null)
                        StartVolley(self, world, target);
                    else
                        Enter(self, Action.SweepRest, sweepRestTime);
                    break;
                default:
                    _attackTimer = streamInterval;
                    _showerTimer = rockShowerInterval * 0.5f;
                    _spiralTimer = spiralInterval * 0.5f;
                    Enter(self, Action.Orbiting, float.MaxValue);
                    break;
            }
        }

        void Dive(Enemy self, PlayerShip target)
        {
            _eruptTarget = target != null ? target.Position : self.Position;
            if (eruptTelegraphPrefab != null)
            {
                _telegraph = Instantiate(eruptTelegraphPrefab, PlaneMath.ToWorld(_eruptTarget), Quaternion.identity);
                _telegraphView = _telegraph.GetComponent<Presentation.ImpactTelegraph>();
                _telegraphView?.Launch(_eruptTarget, _eruptTarget, eruptRadius);
            }
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

            // Two rings, offset by half a step: fast rocks first, then a slower ring to weave through.
            float offset = self.Random.Range(0f, 360f);
            FireRing(self, world, _eruptTarget, rockCount, rockSpeed, offset);
            FireRing(self, world, _eruptTarget, rockCount, slowRockSpeed, offset + 180f / rockCount);
            if (eruptEffect != null)
                Instantiate(eruptEffect, PlaneMath.ToWorld(_eruptTarget, 0.5f), Quaternion.identity);
            if (eruptBurstPrefab != null)
            {
                var burst = Instantiate(eruptBurstPrefab, PlaneMath.ToWorld(_eruptTarget), Quaternion.identity);
                if (burst.TryGetComponent(out Presentation.RingPulse pulse))
                    pulse.SizeMultiplier = eruptRadius;
            }
            GameAudio.Play(SoundCue.Erupt, PlaneMath.ToWorld(_eruptTarget));
            Presentation.CameraRig.Kick(0.45f);

            Enter(self, Action.Emerging, emergeTime, dust: true);
        }

        void StartVolley(Enemy self, CombatWorld world, PlayerShip target)
        {
            _sweepsLeft = sweepsPerVolley;
            PrepareSweep(self, world, target, firstSweepWindUp);
        }

        /// <summary>
        /// Lines up a charge from where the head is, through the ship's current position, to the far side of the arena.
        /// It never teleports, so it is always in sight. The lane shows during the wind-up.
        /// </summary>
        void PrepareSweep(Enemy self, CombatWorld world, PlayerShip target, float windUp)
        {
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(2f, 2f);
            Vector2 from = self.Position;
            Vector2 toTarget = target.Position - from;
            Vector2 axis = toTarget.sqrMagnitude > 1f ? toTarget.normalized : PlaneMath.FromHeading(self.Random.Range(0f, 360f));
            Vector2 through = world.ClampToArena(target.Position, 2.5f);

            _sweepFrom = from;
            _sweepTo = through + axis * DistanceToEdge(through, axis, half);
            Enter(self, Action.SweepWindUp, windUp);
        }

        static float DistanceToEdge(Vector2 from, Vector2 direction, Vector2 half)
        {
            float tx = Mathf.Abs(direction.x) > 1e-4f ? ((direction.x > 0f ? half.x : -half.x) - from.x) / direction.x : float.MaxValue;
            float ty = Mathf.Abs(direction.y) > 1e-4f ? ((direction.y > 0f ? half.y : -half.y) - from.y) / direction.y : float.MaxValue;
            return Mathf.Max(0f, Mathf.Min(tx, ty));
        }

        void FireFan(Enemy self, CombatWorld world, Vector2 forward, int count, float spread, float speed)
        {
            Vector2 mouth = self.Position + forward * (self.Radius + 0.3f);
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (count - 1f) - 0.5f : 0f;
                SpawnBullet(self, world, mouth, PlaneMath.Rotate(forward, t * spread), speed, 26f);
            }
        }

        void FireRing(Enemy self, CombatWorld world, Vector2 center, int count, float speed, float? offsetDegrees = null)
        {
            float offset = offsetDegrees ?? self.Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = PlaneMath.FromHeading(offset + i * 360f / count);
                SpawnBullet(self, world, center + direction * 1.2f, direction, speed, 26f);
            }
        }

        void SpawnBullet(Enemy self, CombatWorld world, Vector2 position, Vector2 direction, float speed, float range) =>
            world.Projectiles.Spawn(new ProjectileSpawn(position, direction, speed, bulletDamage, 0.28f, range,
                self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));

        void Enter(Enemy self, Action action, float duration, bool dust = false)
        {
            _action = action;
            _timer = _actionDuration = duration;
            self.Burrowed = action == Action.Underground;
            self.IsTelegraphing = action == Action.Roar || action == Action.Diving || action == Action.SweepWindUp || action == Action.SpiralWindUp;
            if (dust && burrowDustPrefab != null)
                Instantiate(burrowDustPrefab, PlaneMath.ToWorld(self.Position, 0.3f), Quaternion.identity);
        }

        /// <summary>The shell is armour. Weak points take hits unless it is roaring; the head only once its maw is exposed.</summary>
        void UpdateArmour(Enemy self)
        {
            self.Armoured = !IsMawExposed;
            if (exposedMawMarker != null && exposedMawMarker.activeSelf != IsMawExposed)
                exposedMawMarker.SetActive(IsMawExposed);
            foreach (var segment in _segments)
            {
                if (segment != null)
                    segment.Armoured = !segment.IsWeakPoint || IsRoaring;
            }
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
                float taper = Mathf.Lerp(1f, 0.65f, i / Mathf.Max(1f, segmentCount - 1f));
                bool weakPoint = (i + 1) % weakPointEvery == 0;
                segment.SetSize(taper, weakPoint ? 1.3f : 0.9f);
                segment.Attach(self, weakPoint);
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
                _segments[s].Follow(position, facing, SinkDepth);
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
