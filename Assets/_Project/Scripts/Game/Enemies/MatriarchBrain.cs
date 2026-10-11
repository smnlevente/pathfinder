using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// The Hive Matriarch, guardian of the Veil Nebula and the last boss of a run (GDD §7.5): an alien queen half-fused
    /// with the nebula. Her carapace is armour; her yellow heart is open, and she can be hurt, except while she roars,
    /// charges or gathers a pulse.
    /// <list type="number">
    /// <item><b>Brood</b> (above 66%): she drifts along the top of the arena, births broods of Broodlings, lobs spore
    /// pods that land where the ship was (a ring closes in first, like the Quarry Maw's eruption) and burst, hurting
    /// whoever is inside, into clouds that burn and slow the ship, and sprays a flowing, sweeping bullet stream.</item>
    /// <item><b>Frenzy</b> (66–33%): volleys of two charges through the ship, each shown by a chevron lane and ending
    /// in a two-speed ring burst; then she rests, nearly still. Her heart is open while she lines a charge up and while
    /// she rests; it shuts only while she charges (playtest 7: the rest alone was too short a window).</item>
    /// <item><b>Bloom</b> (below 33%): she holds the arena centre and spins a wobbling two-arm spiral, and every few
    /// seconds gathers herself and sends out a solid expanding ring with no gap: the only way through is to dash.</item>
    /// </list>
    /// Each phase opens with a roar. Her brood dies with her.
    /// </summary>
    public sealed class MatriarchBrain : EnemyBrain, IChargeTelegraph
    {
        public enum Phase { Brood, Frenzy, Bloom }

        enum Action { Roar, Drift, ChargeWindUp, Charging, Rest, Bloom, PulseWindUp }

        struct Spore
        {
            public Vector2 Position;
            public float Timer;
            public bool Landed;
            public GameObject View;
            public Presentation.ImpactTelegraph Mark;
            public Presentation.SporeCloud Cloud;
        }

        struct Pulse
        {
            public Vector2 Centre;
            public float Radius;
            public List<int> Hit;
            public Presentation.RingLine View;
        }

        [Header("Body")]
        [SerializeField, Tooltip("Shown while her heart is open and she can be hurt.")] GameObject heartMarker;

        [Header("Phase 1: Brood")]
        [SerializeField] EnemyDefinition broodling;
        [SerializeField, Min(0.5f)] float broodInterval = 6f;
        [SerializeField, Min(1)] int broodSize = 5;
        [SerializeField, Min(1)] int maxBroodlings = 12;
        [SerializeField, Min(0f), Tooltip("How far she drifts left and right of the centre line.")] float driftRange = 9f;
        [SerializeField] float driftLine = 6f;
        [SerializeField, Min(0.02f)] float streamInterval = 0.12f;
        [SerializeField, Min(0f), Tooltip("The stream sweeps this many degrees either side of the ship.")] float streamSweep = 50f;
        [SerializeField, Min(0.1f)] float streamSpeed = 7f;

        [Header("Spores")]
        [SerializeField, Min(0.5f)] float sporeInterval = 4f;
        [SerializeField, Min(0.1f), Tooltip("Flight time: the landing ring closes in over it.")] float sporeFlightTime = 1.2f;
        [SerializeField, Min(0.1f)] float sporeRadius = 2.5f;
        [SerializeField, Min(0.1f)] float sporeDuration = 6f;
        [SerializeField, Min(0f), Tooltip("Damage to a ship caught where a pod bursts.")] float sporeImpactDamage = 15f;
        [SerializeField, Min(0f), Tooltip("Damage each burn tick inside a cloud (playtest 7: the clouds used to only drain the shield, which nobody noticed).")]
        float sporeBurnDamage = 5f;
        [SerializeField, Min(0.1f)] float sporeBurnInterval = 0.5f;
        [SerializeField, Range(0.1f, 1f)] float sporeSlow = 0.5f;
        [SerializeField] GameObject sporeMarkPrefab;
        [SerializeField] GameObject sporeCloudPrefab;
        [SerializeField, Tooltip("A one-shot flash where a pod bursts.")] GameObject sporeBurstPrefab;

        [Header("Phase 2: Frenzy")]
        [SerializeField, Min(0f)] float firstChargeWindUp = 1.1f;
        [SerializeField, Min(0f)] float chainedChargeWindUp = 0.8f;
        [SerializeField, Min(0.1f)] float chargeSpeed = 16f;
        [SerializeField, Min(1)] int chargesPerVolley = 2;
        [SerializeField, Min(0f)] float restTime = 5f;
        [SerializeField, Min(3)] int burstBullets = 20;

        [Header("Phase 3: Bloom")]
        [SerializeField, Min(0.5f)] float pulseInterval = 3.5f;
        [SerializeField, Min(0f)] float pulseWindUp = 0.8f;
        [SerializeField, Min(0.1f)] float pulseSpeed = 9f;
        [SerializeField, Min(0.05f), Tooltip("How thick the ring's damaging band is.")] float pulseThickness = 0.8f;
        [SerializeField, Min(0f)] float pulseDamage = 20f;
        [SerializeField] Presentation.RingLine pulsePrefab;
        [SerializeField, Min(1)] int spiralArms = 2;
        [SerializeField] float spiralTurnRate = 110f;
        [SerializeField, Min(0.02f)] float spiralInterval = 0.13f;
        [SerializeField, Min(0.1f)] float spiralSpeed = 6f;

        [Header("Bullets")]
        [SerializeField, Min(0f)] float bulletDamage = 10f;
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] GameObject bulletImpactPrefab;

        [Header("Roar")]
        [SerializeField, Min(0f)] float roarTime = 1.5f;

        readonly List<Spore> _spores = new List<Spore>();
        readonly List<Pulse> _pulses = new List<Pulse>();
        readonly List<Enemy> _brood = new List<Enemy>();
        Action _action;
        float _timer, _actionDuration, _time;
        float _broodTimer, _sporeTimer, _streamTimer, _pulseTimer, _spiralTimer, _spiralAngle, _burnTimer;
        Vector2 _chargeFrom, _chargeTo;
        int _chargesLeft;
        bool _subscribed;

        public Phase CurrentPhase { get; private set; }
        public bool IsRoaring => _action == Action.Roar;

        /// <summary>Her heart is open and she can be hurt: always, except while she roars, charges or gathers a pulse.</summary>
        public bool HeartOpen => _action is not (Action.Roar or Action.Charging or Action.PulseWindUp);

        public int LivingBrood => _brood.Count;
        public int ActiveSpores => _spores.Count;
        public int ActivePulses => _pulses.Count;

        /// <summary>The newest bloom pulse's centre and radius, if one is expanding. For tests.</summary>
        public bool TryGetPulse(out Vector2 centre, out float radius)
        {
            centre = Vector2.zero;
            radius = 0f;
            if (_pulses.Count == 0)
                return false;
            centre = _pulses[_pulses.Count - 1].Centre;
            radius = _pulses[_pulses.Count - 1].Radius;
            return true;
        }

        /// <summary>A spore pod is in the air and at least this far along its flight (0..1). For tests and screenshots.</summary>
        public bool HasIncomingSpore(float minProgress)
        {
            foreach (var spore in _spores)
            {
                if (!spore.Landed && 1f - spore.Timer / sporeFlightTime >= minProgress)
                    return true;
            }
            return false;
        }

        /// <summary>The spore clouds that have landed, for tests.</summary>
        public IEnumerable<Vector2> LandedSpores
        {
            get
            {
                foreach (var spore in _spores)
                {
                    if (spore.Landed)
                        yield return spore.Position;
                }
            }
        }

        bool IChargeTelegraph.ShowsCharge => _action == Action.ChargeWindUp;
        Vector2 IChargeTelegraph.ChargeStart => _chargeFrom;
        Vector2 IChargeTelegraph.ChargeDirection => (_chargeTo - _chargeFrom).normalized;
        float IChargeTelegraph.ChargeLength => Vector2.Distance(_chargeFrom, _chargeTo);
        float IChargeTelegraph.ChargeProgress => _actionDuration > 0f ? Mathf.Clamp01(1f - _timer / _actionDuration) : 1f;

        int _selfId;

        public override void ResetState(Enemy self)
        {
            _selfId = self.EntityId;
            _burnTimer = 0f;
            CurrentPhase = Phase.Brood;
            _time = 0f;
            _broodTimer = broodInterval * 0.4f;
            _sporeTimer = sporeInterval * 0.6f;
            _streamTimer = streamInterval;
            Enter(self, Action.Drift, float.MaxValue);
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            if (!_subscribed)
            {
                _subscribed = true;
                self.Died += (entity, info) => OnDied();
            }

            _time += deltaTime;
            _brood.RemoveAll(enemy => enemy == null || !enemy.IsAlive);
            var target = Target(self);
            Vector2 desired = Vector2.zero;

            var phase = PhaseFor(self.Vitals.Hull / self.Vitals.MaxHull);
            if (phase != CurrentPhase && _action != Action.Charging)
            {
                CurrentPhase = phase;
                Enter(self, Action.Roar, roarTime);
                GameAudio.Play(SoundCue.Roar, transform.position);
                Presentation.CameraRig.Kick(0.25f);
            }

            float attackDelta = deltaTime * self.AttackRate;
            _timer -= deltaTime;
            switch (_action)
            {
                case Action.Roar:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 6f;
                    if (_timer <= 0f)
                        StartPhase(self, world, target);
                    break;

                case Action.Drift:
                {
                    // Drift along the top of the arena, sweeping a flowing stream at the ship.
                    var spot = new Vector2(Mathf.Sin(_time * 0.35f) * driftRange, driftLine);
                    desired = Vector2.ClampMagnitude(spot - self.Position, 1f) * self.MoveSpeed;
                    if (target != null)
                    {
                        self.FaceTowards(target.Position - self.Position);
                        if (Tick(ref _streamTimer, attackDelta, streamInterval))
                        {
                            float sweep = streamSweep * Mathf.Sin(_time * 2.2f);
                            SpawnBullet(self, world, PlaneMath.Rotate((target.Position - self.Position).normalized, sweep), streamSpeed);
                        }
                    }
                    if (Tick(ref _broodTimer, attackDelta, broodInterval))
                        Brood(self, world, broodSize);
                    if (target != null && Tick(ref _sporeTimer, attackDelta, sporeInterval))
                        LaunchSpore(self, world, target.Position);
                    break;
                }

                case Action.ChargeWindUp:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 20f;
                    self.FaceTowards(_chargeTo - _chargeFrom);
                    if (_timer <= 0f)
                        Enter(self, Action.Charging, Vector2.Distance(_chargeFrom, _chargeTo) / chargeSpeed + 0.5f);
                    break;

                case Action.Charging:
                {
                    Vector2 axis = (_chargeTo - _chargeFrom).normalized;
                    self.SpeedMultiplier = chargeSpeed / Mathf.Max(self.MoveSpeed, 0.01f);
                    self.AccelerationMultiplier = 30f;
                    self.ContactDamageMultiplier = 1.5f;
                    self.FaceTowards(axis);
                    desired = axis * chargeSpeed;
                    float along = Vector2.Dot(self.Position - _chargeFrom, axis);
                    if (along >= Vector2.Distance(_chargeFrom, _chargeTo) - 0.5f || _timer <= 0f)
                    {
                        Burst(self, world);
                        if (--_chargesLeft > 0 && target != null)
                            PrepareCharge(self, world, target, chainedChargeWindUp);
                        else
                            Enter(self, Action.Rest, restTime);
                    }
                    break;
                }

                case Action.Rest:
                    // Tired, heart open, barely moving: the window to hurt her. She keeps spores coming.
                    self.SpeedMultiplier = 0.15f;
                    if (target != null)
                    {
                        desired = (target.Position - self.Position).normalized * self.MoveSpeed;
                        if (Tick(ref _sporeTimer, attackDelta, sporeInterval * 1.5f))
                            LaunchSpore(self, world, target.Position);
                    }
                    if (_timer <= 0f && target != null)
                    {
                        _chargesLeft = chargesPerVolley;
                        PrepareCharge(self, world, target, firstChargeWindUp);
                    }
                    break;

                case Action.Bloom:
                    // Hold the centre (hurrying back there first), spin the spiral, and every few seconds gather a pulse.
                    self.SpeedMultiplier = self.Position.sqrMagnitude > 4f ? 2f : 1f;
                    desired = Vector2.ClampMagnitude(-self.Position, 1f) * self.MoveSpeed * self.SpeedMultiplier;
                    if (Tick(ref _spiralTimer, attackDelta, spiralInterval))
                        Spiral(self, world);
                    if (Tick(ref _broodTimer, attackDelta, broodInterval * 1.5f))
                        Brood(self, world, Mathf.Max(1, broodSize / 2));
                    if (Tick(ref _pulseTimer, attackDelta, pulseInterval))
                        Enter(self, Action.PulseWindUp, pulseWindUp);
                    break;

                case Action.PulseWindUp:
                    self.SpeedMultiplier = 0f;
                    self.AccelerationMultiplier = 10f;
                    if (_timer <= 0f)
                    {
                        SendPulse(self);
                        Enter(self, Action.Bloom, float.MaxValue);
                    }
                    break;
            }

            UpdateSpores(world, deltaTime);
            UpdatePulses(self, deltaTime);
            self.Armoured = !HeartOpen;
            if (heartMarker != null && heartMarker.activeSelf != HeartOpen)
                heartMarker.SetActive(HeartOpen);
            return desired;
        }

        static Phase PhaseFor(float health) => health > 2f / 3f ? Phase.Brood : health > 1f / 3f ? Phase.Frenzy : Phase.Bloom;

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
                case Phase.Brood:
                    Enter(self, Action.Drift, float.MaxValue);
                    break;
                case Phase.Frenzy:
                    _chargesLeft = chargesPerVolley;
                    if (target != null)
                        PrepareCharge(self, world, target, firstChargeWindUp);
                    else
                        Enter(self, Action.Rest, restTime);
                    break;
                default:
                    _pulseTimer = pulseInterval * 0.5f;
                    _spiralAngle = self.Random.Range(0f, 360f);
                    Enter(self, Action.Bloom, float.MaxValue);
                    break;
            }
        }

        void Enter(Enemy self, Action action, float duration)
        {
            _action = action;
            _timer = _actionDuration = duration;
            self.IsTelegraphing = action is Action.Roar or Action.ChargeWindUp or Action.PulseWindUp;
        }

        // ---------------------------------------------------------------- brood and spores

        void Brood(Enemy self, CombatWorld world, int count)
        {
            if (broodling == null)
                return;
            for (int i = 0; i < count && _brood.Count < maxBroodlings; i++)
            {
                Vector2 offset = PlaneMath.FromHeading(self.Random.Range(0f, 360f)) * (self.Radius + 1f);
                _brood.Add(world.SpawnEnemy(broodling, world.ClampToArena(self.Position + offset, 0.5f), dropsLoot: false));
            }
        }

        void LaunchSpore(Enemy self, CombatWorld world, Vector2 at)
        {
            var spore = new Spore { Position = world.ClampToArena(at, sporeRadius * 0.5f), Timer = sporeFlightTime };
            if (sporeMarkPrefab != null)
            {
                spore.View = Instantiate(sporeMarkPrefab, PlaneMath.ToWorld(spore.Position), Quaternion.identity);
                spore.Mark = spore.View.GetComponent<Presentation.ImpactTelegraph>();
                spore.Mark?.Launch(self.Position, spore.Position, sporeRadius);
            }
            _spores.Add(spore);
            GameAudio.Play(SoundCue.SporePod, transform.position);
        }

        void UpdateSpores(CombatWorld world, float deltaTime)
        {
            for (int i = _spores.Count - 1; i >= 0; i--)
            {
                var spore = _spores[i];
                spore.Timer -= deltaTime;
                if (!spore.Landed)
                {
                    spore.Mark?.Show(spore.Position, 1f - spore.Timer / sporeFlightTime, spore.Timer < 0.3f);
                    if (spore.Timer <= 0f)
                    {
                        // The pod bursts, hitting whoever stayed under it, and leaves a cloud that burns and slows.
                        BurstSpore(spore.Position);
                        if (spore.View != null)
                            Destroy(spore.View);
                        spore.Mark = null;
                        spore.View = sporeCloudPrefab != null ? Instantiate(sporeCloudPrefab, PlaneMath.ToWorld(spore.Position), Quaternion.identity) : null;
                        spore.Cloud = spore.View != null ? spore.View.GetComponent<Presentation.SporeCloud>() : null;
                        spore.Cloud?.Begin(sporeRadius);
                        spore.Landed = true;
                        spore.Timer = sporeDuration;
                    }
                }
                else
                {
                    spore.Cloud?.SetLife(sporeDuration - spore.Timer, spore.Timer);
                    foreach (var player in CombatRegistry.Players)
                    {
                        if (player.IsAlive && InSpore(player, spore.Position))
                            player.Slow(sporeSlow, 0.15f);
                    }
                    if (spore.Timer <= 0f)
                    {
                        if (spore.View != null)
                            Destroy(spore.View);
                        _spores.RemoveAt(i);
                        continue;
                    }
                }
                _spores[i] = spore;
            }
            BurnInClouds(deltaTime);
        }

        bool InSpore(PlayerShip player, Vector2 centre) =>
            (player.Position - centre).sqrMagnitude <= (sporeRadius + player.Radius) * (sporeRadius + player.Radius);

        void BurstSpore(Vector2 at)
        {
            GameAudio.Play(SoundCue.Erupt, PlaneMath.ToWorld(at));
            if (sporeBurstPrefab != null)
            {
                var burst = Instantiate(sporeBurstPrefab, PlaneMath.ToWorld(at), Quaternion.identity);
                if (burst.TryGetComponent(out Presentation.RingPulse pulse))
                    pulse.SizeMultiplier = sporeRadius;
            }
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive && InSpore(player, at))
                    HurtWithSpores(player, at, sporeImpactDamage);
            }
        }

        /// <summary>Every burn tick, each ship inside any cloud takes one hit: overlapping clouds don't stack.</summary>
        void BurnInClouds(float deltaTime)
        {
            _burnTimer -= deltaTime;
            if (_burnTimer > 0f)
                return;
            _burnTimer = sporeBurnInterval;
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive)
                    continue;
                foreach (var spore in _spores)
                {
                    if (spore.Landed && InSpore(player, spore.Position))
                    {
                        HurtWithSpores(player, spore.Position, sporeBurnDamage);
                        break;
                    }
                }
            }
        }

        void HurtWithSpores(PlayerShip player, Vector2 from, float damage)
        {
            Vector2 away = player.Position - from;
            Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
            player.ApplyDamage(new DamageInfo(damage, Team.Enemy, _selfId, player.Position, direction, DamageKind.Contact));
        }

        // ---------------------------------------------------------------- charges

        void PrepareCharge(Enemy self, CombatWorld world, PlayerShip target, float windUp)
        {
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(2.5f, 2.5f);
            Vector2 toTarget = target.Position - self.Position;
            Vector2 axis = toTarget.sqrMagnitude > 1f ? toTarget.normalized : PlaneMath.FromHeading(self.Random.Range(0f, 360f));
            Vector2 through = world.ClampToArena(target.Position, 3f);
            _chargeFrom = self.Position;
            _chargeTo = through + axis * DistanceToEdge(through, axis, half);
            Enter(self, Action.ChargeWindUp, windUp);
        }

        static float DistanceToEdge(Vector2 from, Vector2 direction, Vector2 half)
        {
            float tx = Mathf.Abs(direction.x) > 1e-4f ? ((direction.x > 0f ? half.x : -half.x) - from.x) / direction.x : float.MaxValue;
            float ty = Mathf.Abs(direction.y) > 1e-4f ? ((direction.y > 0f ? half.y : -half.y) - from.y) / direction.y : float.MaxValue;
            return Mathf.Max(0f, Mathf.Min(tx, ty));
        }

        /// <summary>A two-speed ring: alternate bullets fly slower, so it arrives as two interleaved waves.</summary>
        void Burst(Enemy self, CombatWorld world)
        {
            float offset = self.Random.Range(0f, 360f);
            for (int i = 0; i < burstBullets; i++)
                SpawnBullet(self, world, PlaneMath.FromHeading(offset + i * 360f / burstBullets), i % 2 == 0 ? 6.5f : 4.5f);
            Presentation.CameraRig.Kick(0.2f);
        }

        // ---------------------------------------------------------------- bloom

        void Spiral(Enemy self, CombatWorld world)
        {
            _spiralAngle += spiralTurnRate * spiralInterval;
            float wobble = 20f * Mathf.Sin(_time * 3f);
            for (int arm = 0; arm < spiralArms; arm++)
                SpawnBullet(self, world, PlaneMath.FromHeading(_spiralAngle + wobble + arm * 360f / spiralArms), spiralSpeed);
        }

        void SendPulse(Enemy self)
        {
            var pulse = new Pulse { Centre = self.Position, Radius = self.Radius, Hit = new List<int>() };
            if (pulsePrefab != null)
            {
                pulse.View = Instantiate(pulsePrefab);
                pulse.View.Show(pulse.Centre, pulse.Radius);
            }
            _pulses.Add(pulse);
            GameAudio.Play(SoundCue.BloomPulse, transform.position);
            Presentation.CameraRig.Kick(0.3f);
        }

        /// <summary>Each pulse expands at a steady speed; its band hits each ship once, unless the ship dashes through it.</summary>
        void UpdatePulses(Enemy self, float deltaTime)
        {
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                var pulse = _pulses[i];
                pulse.Radius += pulseSpeed * deltaTime;
                foreach (var player in CombatRegistry.Players)
                {
                    if (!player.IsAlive || player.IsIntangible || pulse.Hit.Contains(player.EntityId))
                        continue;
                    Vector2 away = player.Position - pulse.Centre;
                    if (Mathf.Abs(away.magnitude - pulse.Radius) > pulseThickness * 0.5f + player.Radius)
                        continue;
                    pulse.Hit.Add(player.EntityId);
                    Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                    player.ApplyDamage(new DamageInfo(pulseDamage, self.Team, self.EntityId, pulse.Centre + direction * pulse.Radius, direction, DamageKind.Contact));
                }
                pulse.View?.Show(pulse.Centre, pulse.Radius);
                if (pulse.Radius > 45f)
                {
                    if (pulse.View != null)
                        Destroy(pulse.View.gameObject);
                    _pulses.RemoveAt(i);
                    continue;
                }
                _pulses[i] = pulse;
            }
        }

        void SpawnBullet(Enemy self, CombatWorld world, Vector2 direction, float speed) =>
            world.Projectiles.Spawn(new ProjectileSpawn(self.Position + direction * (self.Radius + 0.3f), direction, speed, bulletDamage, 0.28f, 40f,
                self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));

        // ---------------------------------------------------------------- death and cleanup

        /// <summary>Her brood dies with her, and her spores and pulses fade.</summary>
        void OnDied()
        {
            foreach (var enemy in new List<Enemy>(_brood))
            {
                if (enemy != null && enemy.IsAlive)
                    enemy.ApplyDamage(new DamageInfo(enemy.Vitals.Hull + enemy.Vitals.Shield + 1f, Team.Player, 0, enemy.Position, Vector2.zero, DamageKind.Proc));
            }
            _brood.Clear();
            ClearHazards();
        }

        void ClearHazards()
        {
            foreach (var spore in _spores)
            {
                if (spore.View != null)
                    Destroy(spore.View);
            }
            _spores.Clear();
            foreach (var pulse in _pulses)
            {
                if (pulse.View != null)
                    Destroy(pulse.View.gameObject);
            }
            _pulses.Clear();
        }

        void OnDestroy() => ClearHazards();
    }
}
