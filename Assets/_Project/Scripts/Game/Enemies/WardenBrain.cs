using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// The Binary Wardens, guardians of the second galaxy (GDD §7.5): one AI core that splits in two. The cores circle the
    /// arena centre on opposite sides, joined by a damaging tether that sweeps round like a clock hand, while the orbit
    /// slowly grows and shrinks. Alpha fires rotating rings, Beta aimed fans.
    /// <para>A core brought to zero while its twin is up only goes offline: armoured and sunk to the floor, it reboots after
    /// a few seconds at half health, and its twin enrages meanwhile. Both fall only when the second goes down while the
    /// first is still offline, so the pilot has to wear both down evenly and finish them together.</para>
    /// The Alpha is the level's boss; it spawns its twin on its first tick.
    /// </summary>
    public sealed class WardenBrain : EnemyBrain, IDeathGuard, IMultiPartBoss, IAreaTelegraph, ISinkDepth
    {
        public enum Role { Alpha, Beta }

        [Header("Split")]
        [SerializeField, Tooltip("Alpha only: the twin it splits off. Leave empty on the Beta.")] EnemyDefinition partnerDefinition;
        [SerializeField, Min(0f), Tooltip("Armoured while the cores separate at the start.")] float splitTime = 2f;

        [Header("Orbit")]
        [SerializeField, Min(1f), Tooltip("Small enough that both cores fit on screen from the middle of the arena.")] float orbitRadius = 7f;
        [SerializeField, Min(0f), Tooltip("The orbit grows and shrinks by this much.")] float orbitBreath = 1.5f;
        [SerializeField, Min(1f)] float breathPeriod = 16f;
        [SerializeField, Tooltip("Degrees per second.")] float orbitSpeed = 22f;
        [SerializeField, Min(0.1f), Tooltip("How hard a core steers back to its place in the orbit.")] float slotGain = 3f;

        [Header("Tether")]
        [SerializeField, Min(0f)] float tetherDamage = 15f;
        [SerializeField, Min(0f)] float tetherHalfWidth = 0.25f;
        [SerializeField, Min(0f), Tooltip("After a reboot the tether flickers harmlessly for this long before it bites.")] float relinkTime = 1.2f;

        [Header("Offline")]
        [SerializeField, Min(0.5f)] float rebootTime = 5f;
        [SerializeField, Range(0.05f, 1f)] float rebootHealth = 0.5f;
        [SerializeField, Min(1f), Tooltip("Attack speed of a core whose twin is offline.")] float enrageRate = 1.5f;
        [SerializeField, Min(0.5f), Tooltip("Radius of the reboot ring on the floor.")] float rebootRingRadius = 2.2f;
        [SerializeField] GameObject offlineEffect;
        [SerializeField] GameObject rebootEffect;

        [Header("Attacks")]
        [SerializeField, Min(0f)] float volleyTelegraph = 0.4f;
        [SerializeField, Min(0.2f), Tooltip("Alpha: seconds between rings.")] float ringInterval = 2.6f;
        [SerializeField, Min(3)] int ringBullets = 14;
        [SerializeField, Tooltip("Each ring turns this many degrees from the last.")] float ringTurn = 9f;
        [SerializeField, Min(0.1f)] float ringSpeed = 6f;
        [SerializeField, Min(0.2f), Tooltip("Beta: seconds between aimed fans.")] float fanInterval = 2f;
        [SerializeField, Min(1)] int fanBullets = 5;
        [SerializeField, Min(0f)] float fanSpread = 36f;
        [SerializeField, Min(0.1f)] float fanSpeed = 8f;
        [SerializeField, Min(0f)] float bulletDamage = 10f;
        [SerializeField] GameObject bulletPrefab;

        /// <summary>What this enemy shoots (playtest 7: each shooter has its own kind of shot).</summary>
        public GameObject BulletPrefab => bulletPrefab;
        [SerializeField] GameObject bulletImpactPrefab;

        const float StartHeading = 90f; // Alpha takes the right side of the arena, Beta the left

        Enemy _self;
        WardenBrain _twin;
        Enemy _twinEnemy;
        bool _linked;
        bool _offline;
        bool _fallingWithTwin;
        bool _tetherWasShown;
        float _time;
        float _splitLeft;
        float _relinkLeft;
        float _rebootLeft;
        float _volleyTimer;
        float _telegraphLeft;
        float _ringHeading;
        float _sink;

        public Role CurrentRole { get; private set; }
        public WardenBrain Twin => _twinEnemy != null ? _twin : null;
        public Enemy TwinEnemy => _twinEnemy != null ? _twinEnemy : null;

        /// <summary>Up and fighting: alive and not offline.</summary>
        public bool IsOnline => _self != null && _self.IsAlive && !_offline;
        public bool IsOffline => _offline;
        public bool IsSplitting => _splitLeft > 0f;

        /// <summary>Seconds until an offline core reboots; 0 while it is online.</summary>
        public float RebootRemaining => _offline ? _rebootLeft : 0f;

        /// <summary>Drawn by the Alpha while both cores are up. It flickers, harmless, while relinking.</summary>
        public bool TetherShown => CurrentRole == Role.Alpha && Twin != null && IsOnline && _twin.IsOnline && !IsSplitting && !_twin.IsSplitting;
        public bool TetherActive => TetherShown && _relinkLeft <= 0f;

        float IAreaTelegraph.AreaRadius => rebootRingRadius;
        float IAreaTelegraph.AreaArc => 360f;
        bool IAreaTelegraph.ShowsArea => _offline;
        float IAreaTelegraph.AreaProgress => Mathf.Clamp01(1f - _rebootLeft / rebootTime);

        /// <summary>Offline cores sink part-way into the floor.</summary>
        public float SinkDepth => _sink;

        int IMultiPartBoss.PartCount => _linked ? 2 : 1;
        Enemy IMultiPartBoss.Part(int index) => PartBrain(index) is { } brain ? brain._self : null;
        string IMultiPartBoss.PartName(int index) => Loc.T(index == 0 ? "hud.boss.alpha" : "hud.boss.beta");
        float IMultiPartBoss.PartDowntime(int index) => PartBrain(index) is { } brain ? brain.RebootRemaining : 0f;

        WardenBrain PartBrain(int index)
        {
            bool mine = (index == 0) == (CurrentRole == Role.Alpha);
            if (mine)
                return _self != null ? this : null;
            return Twin;
        }

        public override void ResetState(Enemy self)
        {
            _self = self;
            CurrentRole = Role.Alpha;
            _offline = _fallingWithTwin = _tetherWasShown = false;
            _time = _relinkLeft = _rebootLeft = _telegraphLeft = _sink = 0f;
            _splitLeft = splitTime;
            _ringHeading = self.Random.Range(0f, 360f);
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            if (!_linked && CurrentRole == Role.Alpha && partnerDefinition != null)
                Split(self, world);

            // The Beta keeps time with the Alpha, so they stay exactly opposite. A lone core keeps its own clock.
            if (CurrentRole == Role.Beta && Twin != null)
                _time = _twin._time;
            else
                _time += deltaTime;

            if (_splitLeft > 0f)
                _splitLeft -= deltaTime;
            if (_offline)
            {
                _rebootLeft -= deltaTime;
                if (_rebootLeft <= 0f)
                    Reboot(self);
            }

            self.Armoured = _offline || IsSplitting;
            _sink = Mathf.MoveTowards(_sink, _offline ? 0.35f : 0f, deltaTime * 2f);

            UpdateTether(self, deltaTime);
            var target = Target(self);
            if (target != null)
                self.FaceTowards(target.Position - self.Position);
            if (IsOnline && !IsSplitting && target != null)
                UpdateAttacks(self, deltaTime, world, target.Position);
            else
            {
                _telegraphLeft = 0f;
                self.IsTelegraphing = false;
            }

            if (_offline)
            {
                // Powered down: it drifts to a halt where it fell and can't hurt by touch.
                self.SpeedMultiplier = 0f;
                self.AccelerationMultiplier = 3f;
                self.ContactDamageMultiplier = 0f;
                return Vector2.zero;
            }

            // Fly to its place in the orbit, leading the slot so it doesn't trail behind.
            Vector2 slot = Slot(_time), next = Slot(_time + deltaTime);
            self.SpeedMultiplier = 1.8f;
            return (next - slot) / deltaTime + (slot - self.Position) * slotGain;
        }

        /// <summary>Where this core belongs in the orbit at a given time.</summary>
        Vector2 Slot(float time)
        {
            float heading = StartHeading + orbitSpeed * time + (CurrentRole == Role.Beta ? 180f : 0f);
            float radius = orbitRadius + orbitBreath * Mathf.Sin(2f * Mathf.PI * time / breathPeriod);
            return PlaneMath.FromHeading(heading) * radius;
        }

        // ---------------------------------------------------------------- split and link

        void Split(Enemy self, CombatWorld world)
        {
            _linked = true;
            // The twin matches the Alpha's health, whatever galaxy scaling and boss multiplier applied to it.
            float scale = self.Vitals.MaxHull / (partnerDefinition.MaxHealth * Mathf.Max(world.EnemyHealthMultiplier, 0.01f));
            var beta = world.SpawnEnemy(partnerDefinition, self.Position, dropsLoot: false, extraHealthMultiplier: scale);
            if (!beta.TryGetComponent(out WardenBrain betaBrain))
            {
                Debug.LogError($"[WardenBrain] {partnerDefinition.name} needs a {nameof(WardenBrain)}.", this);
                return;
            }
            betaBrain.CurrentRole = Role.Beta;
            betaBrain.Link(this, self);
            Link(betaBrain, beta);
        }

        void Link(WardenBrain twin, Enemy twinEnemy)
        {
            _linked = true;
            _twin = twin;
            _twinEnemy = twinEnemy;
            _volleyTimer = (CurrentRole == Role.Alpha ? ringInterval : fanInterval) * 0.5f;
        }

        void UpdateTether(Enemy self, float deltaTime)
        {
            bool shown = TetherShown;
            if (shown && !_tetherWasShown)
                _relinkLeft = relinkTime; // a new link always flickers first, so it never snaps on across the ship
            _tetherWasShown = shown;
            if (!shown)
                return;
            if (_relinkLeft > 0f)
            {
                _relinkLeft -= deltaTime;
                return;
            }

            Vector2 from = self.Position, to = _twinEnemy.Position;
            float length = Vector2.Distance(from, to);
            if (length <= self.Radius + _twinEnemy.Radius)
                return;
            Vector2 axis = (to - from) / length;
            from += axis * self.Radius;
            to -= axis * _twinEnemy.Radius;

            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive || player.IsIntangible
                    || !PlaneMath.SegmentHitsCircle(from, to, player.Position, player.Radius + tetherHalfWidth, out _))
                    continue;
                Vector2 closest = from + axis * Mathf.Clamp(Vector2.Dot(player.Position - from, axis), 0f, Vector2.Distance(from, to));
                Vector2 away = player.Position - closest;
                Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : new Vector2(-axis.y, axis.x);
                player.ApplyDamage(new DamageInfo(tetherDamage, self.Team, self.EntityId, closest, direction, DamageKind.Contact));
            }
        }

        // ---------------------------------------------------------------- offline and reboot

        public bool TryPreventDeath(Enemy self, in DamageInfo info)
        {
            if (_fallingWithTwin || Twin == null || !_twinEnemy.IsAlive)
                return false;

            if (_twin.IsOnline)
            {
                GoOffline(self);
                return true;
            }

            // Its twin is still offline: the link breaks and both cores fall together.
            _twin.FallWithTwin();
            return false;
        }

        void GoOffline(Enemy self)
        {
            _offline = true;
            _rebootLeft = rebootTime;
            _telegraphLeft = 0f;
            self.IsTelegraphing = false;
            self.Armoured = true;
            if (offlineEffect != null)
                Instantiate(offlineEffect, PlaneMath.ToWorld(self.Position, 0.5f), Quaternion.identity);
            GameAudio.Play(SoundCue.CoreOffline, transform.position);
            Presentation.CameraRig.Kick(0.2f);
        }

        void Reboot(Enemy self)
        {
            _offline = false;
            self.Armoured = IsSplitting;
            self.Vitals.Revive(self.Vitals.MaxHull * rebootHealth);
            _volleyTimer = (CurrentRole == Role.Alpha ? ringInterval : fanInterval) * 0.5f;
            if (rebootEffect != null)
                Instantiate(rebootEffect, PlaneMath.ToWorld(self.Position, 0.5f), Quaternion.identity);
            GameAudio.Play(SoundCue.CoreReboot, transform.position);
        }

        void FallWithTwin()
        {
            if (_self == null || !_self.IsAlive)
                return;
            _fallingWithTwin = true;
            _offline = false;
            _self.Armoured = false;
            _self.ApplyDamage(new DamageInfo(_self.Vitals.Hull + _self.Vitals.Shield + 1f, Team.Player, 0, _self.Position, Vector2.zero, DamageKind.Proc));
        }

        // ---------------------------------------------------------------- attacks

        void UpdateAttacks(Enemy self, float deltaTime, CombatWorld world, Vector2 targetPosition)
        {
            bool enraged = Twin != null && !_twin.IsOnline;
            float attackDelta = deltaTime * self.AttackRate * (enraged ? enrageRate : 1f);

            if (_telegraphLeft > 0f)
            {
                _telegraphLeft -= attackDelta;
                if (_telegraphLeft <= 0f)
                {
                    self.IsTelegraphing = false;
                    if (CurrentRole == Role.Alpha)
                        FireRing(self, world);
                    else
                        FireFan(self, world, targetPosition);
                }
                return;
            }

            _volleyTimer -= attackDelta;
            if (_volleyTimer <= 0f)
            {
                _volleyTimer += CurrentRole == Role.Alpha ? ringInterval : fanInterval;
                _telegraphLeft = Mathf.Max(volleyTelegraph, 1e-3f);
                self.IsTelegraphing = true;
            }
        }

        void FireRing(Enemy self, CombatWorld world)
        {
            _ringHeading += ringTurn;
            for (int i = 0; i < ringBullets; i++)
            {
                Vector2 direction = PlaneMath.FromHeading(_ringHeading + i * 360f / ringBullets);
                SpawnBullet(self, world, self.Position + direction * (self.Radius + 0.3f), direction, ringSpeed);
            }
        }

        void FireFan(Enemy self, CombatWorld world, Vector2 targetPosition)
        {
            Vector2 toTarget = targetPosition - self.Position;
            Vector2 forward = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector2.down;
            for (int i = 0; i < fanBullets; i++)
            {
                float t = fanBullets > 1 ? i / (fanBullets - 1f) - 0.5f : 0f;
                Vector2 direction = PlaneMath.Rotate(forward, t * fanSpread);
                SpawnBullet(self, world, self.Position + forward * (self.Radius + 0.3f), direction, fanSpeed);
            }
        }

        void SpawnBullet(Enemy self, CombatWorld world, Vector2 position, Vector2 direction, float speed) =>
            world.Projectiles.Spawn(new ProjectileSpawn(position, direction, speed, bulletDamage, 0.28f, 30f,
                self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));
    }
}
