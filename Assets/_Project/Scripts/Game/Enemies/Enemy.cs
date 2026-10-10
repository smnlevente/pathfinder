using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Shared enemy body: health, steering, separation, contact damage, loot and death. Behaviour comes from
    /// the <see cref="EnemyBrain"/> on the same GameObject. Elite modifiers scale its stats.
    /// </summary>
    public sealed class Enemy : CombatEntity
    {
        const float ContactCooldown = 0.75f;
        const float SeparationPadding = 0.3f;
        const float KnockbackDrag = 8f; // knockback travels about v / 8 units, whatever the enemy's own acceleration

        [SerializeField] EnemyDefinition definition;
        [SerializeField, Min(0.01f), Tooltip("Seconds to reach full speed.")] float accelerationTime = 0.25f;

        public override Team Team => Team.Enemy;
        public override Vitals Vitals => _vitals;

        public EnemyDefinition Definition => definition;
        public EliteModifierDefinition Elite { get; private set; }
        public bool IsElite => Elite != null;
        public Vector2 Velocity { get; private set; }

        /// <summary>Base move speed, including any elite modifier.</summary>
        public float MoveSpeed => definition.MoveSpeed * (Elite != null ? Elite.MoveSpeedMultiplier : 1f);

        /// <summary>Multiplier for attack timers: 1 normally, higher for Hasty elites.</summary>
        public float AttackRate => Elite != null ? Elite.AttackRateMultiplier : 1f;

        /// <summary>Deterministic per-enemy randomness for its brain (GDD §14.3).</summary>
        public IRandom Random { get; private set; }

        /// <summary>Presentation hint: the enemy is winding up an attack.</summary>
        /// <summary>Winding up a telegraphed attack. Turning it on raises <see cref="CombatEvents.Telegraphed"/>.</summary>
        public bool IsTelegraphing
        {
            get => _telegraphing;
            set
            {
                if (value && !_telegraphing)
                    CombatEvents.RaiseTelegraphed(this);
                _telegraphing = value;
            }
        }

        bool _telegraphing;

        /// <summary>Set by the brain each tick. Allows bursts beyond normal speed, e.g. a Lancer charge.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float AccelerationMultiplier { get; set; } = 1f;
        public float ContactDamageMultiplier { get; set; } = 1f;

        /// <summary>True during the tick after this enemy dealt contact damage.</summary>
        public bool DealtContactDamage { get; private set; }

        /// <summary>Underground or otherwise out of reach, e.g. a burrowed Quarry Maw. Shots pass through and it deals no contact damage.</summary>
        public bool Burrowed { get; set; }

        public override bool IsIntangible => Burrowed;

        Vitals _vitals;
        EnemyBrain _brain;
        CombatWorld _world;
        IRandom _lootRandom;
        bool _dropsLoot;
        float _contactCooldown;
        bool _headingOverridden;
        bool _contactThisTick;
        Vector2 _knockback;
        readonly System.Collections.Generic.List<float> _burns = new System.Collections.Generic.List<float>();
        float _burnDamagePerStack;
        float _burnAccumulated;
        int _burnSourceId;

        protected override void Awake()
        {
            base.Awake();
            _brain = GetComponent<EnemyBrain>();
            if (_brain == null)
                Debug.LogError($"[Enemy] {name} needs an {nameof(EnemyBrain)} component.", this);
            if (definition != null)
                Initialize(null, definition, new Pcg32(0UL, 0UL), new Pcg32(0UL, 1UL), null, dropsLoot: false);
        }

        /// <summary>Use <see cref="CombatWorld.SpawnEnemy"/> rather than calling this directly.</summary>
        public void Initialize(CombatWorld world, EnemyDefinition enemyDefinition, IRandom random, IRandom lootRandom,
            EliteModifierDefinition elite, bool dropsLoot, float healthMultiplier = 1f)
        {
            _world = world;
            definition = enemyDefinition;
            Random = random;
            _lootRandom = lootRandom;
            _dropsLoot = dropsLoot;
            Elite = elite;

            float health = definition.MaxHealth * healthMultiplier * (elite != null ? elite.HealthMultiplier : 1f);
            _vitals = new Vitals(health);
            Velocity = Vector2.zero;
            _knockback = Vector2.zero;
            _burns.Clear();
            _burnAccumulated = 0f;

            float scale = elite != null ? elite.ScaleMultiplier : 1f;
            SetRadiusScale(scale);
            transform.localScale = Vector3.one * scale;
            if (elite != null && elite.MarkerPrefab != null)
            {
                // Markers are authored at unit size. Fit them to this enemy's footprint.
                var marker = Instantiate(elite.MarkerPrefab, transform, worldPositionStays: false).transform;
                marker.localScale *= Radius / scale * 2.8f;
            }

            _brain.ResetState(this);
        }

        /// <summary>Lets the brain aim the body independently of its movement, e.g. a strafing Gunner.</summary>
        public void FaceTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f)
                return;

            Heading = PlaneMath.HeadingDegrees(direction);
            _headingOverridden = true;
        }

        /// <summary>Turns toward a direction by at most the given number of degrees.</summary>
        public void TurnTowards(Vector2 direction, float maxDegrees)
        {
            if (direction.sqrMagnitude < 1e-6f)
                return;

            Heading = Mathf.MoveTowardsAngle(Heading, PlaneMath.HeadingDegrees(direction), maxDegrees);
            _headingOverridden = true;
        }

        /// <summary>Burning, e.g. from Incendiary rounds. Each application is a separate stack with its own timer.</summary>
        public bool IsBurning => _burns.Count > 0;

        /// <summary>Adds a burn stack dealing <paramref name="damagePerSecond"/> for <paramref name="duration"/> seconds (GDD §8.3).</summary>
        public void ApplyBurn(float damagePerSecond, float duration, int sourceId, int maxStacks = int.MaxValue)
        {
            if (_burns.Count >= maxStacks)
            {
                // At the cap, refresh the stack closest to expiring.
                int oldest = 0;
                for (int i = 1; i < _burns.Count; i++)
                {
                    if (_burns[i] < _burns[oldest])
                        oldest = i;
                }
                _burns[oldest] = duration;
            }
            else
            {
                _burns.Add(duration);
            }
            _burnDamagePerStack = damagePerSecond;
            _burnSourceId = sourceId;
        }

        /// <summary>Adds a shove that decays quickly, independent of the enemy's own movement.</summary>
        public void ApplyKnockback(Vector2 velocity) => _knockback += velocity;

        public override void Simulate(float deltaTime, CombatWorld world)
        {
            if (_burns.Count > 0)
            {
                TickBurn(deltaTime);
                if (!IsAlive)
                    return;
            }

            _headingOverridden = false;
            SpeedMultiplier = AccelerationMultiplier = ContactDamageMultiplier = 1f;
            DealtContactDamage = _contactThisTick;
            _contactThisTick = false;

            Vector2 desired = _brain.Think(this, deltaTime, world);
            desired += Separation() * MoveSpeed;

            float maxSpeed = MoveSpeed * SpeedMultiplier;
            // Based on the larger speed, so a brain can also brake to a halt (speed multiplier 0) quickly.
            float acceleration = Mathf.Max(MoveSpeed, maxSpeed) / accelerationTime * AccelerationMultiplier;
            Velocity = Vector2.MoveTowards(Velocity, Vector2.ClampMagnitude(desired, maxSpeed), acceleration * deltaTime);
            _knockback *= Mathf.Max(0f, 1f - KnockbackDrag * deltaTime);
            Position = world.ClampToArena(Position + (Velocity + _knockback) * deltaTime, Radius);

            if (!_headingOverridden && Velocity.sqrMagnitude > 0.01f)
                Heading = PlaneMath.HeadingDegrees(Velocity);

            _contactCooldown -= deltaTime;
            if (_contactCooldown <= 0f && definition.ContactDamage > 0f && !Burrowed)
                TryContactDamage();

            _vitals.Tick(deltaTime);
        }

        /// <summary>Removes the enemy without a kill: no loot and no death event. Used when a level is torn down.</summary>
        public void Despawn()
        {
            _dropsLoot = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        protected override float IncomingDamageMultiplier(in DamageInfo info) =>
            _brain is IDamageModifier modifier ? modifier.IncomingDamageMultiplier(this, info) : 1f;

        protected override void OnKilled(in DamageInfo info)
        {
            if (_dropsLoot && _world != null && Authority.IsAuthority)
                DropLoot();
            Destroy(gameObject);
        }

        void DropLoot()
        {
            int amount = Mathf.RoundToInt(definition.ScrapsDrop * (Elite != null ? Elite.ScrapsMultiplier : 1f) * _world.ScrapsDropMultiplier);
            bool drops = Elite != null || _lootRandom.Chance(definition.ScrapsDropChance);
            if (drops && amount > 0)
                _world.Pickups.SpawnScraps(Position, amount, _lootRandom);
        }

        void TickBurn(float deltaTime)
        {
            _burnAccumulated += _burnDamagePerStack * _burns.Count * deltaTime;
            for (int i = _burns.Count - 1; i >= 0; i--)
            {
                _burns[i] -= deltaTime;
                if (_burns[i] <= 0f)
                    _burns.RemoveAt(i);
            }

            // Apply in whole points so burning doesn't produce a flood of tiny hits.
            if (_burnAccumulated >= 1f || (_burns.Count == 0 && _burnAccumulated > 0f))
            {
                float amount = _burnAccumulated;
                _burnAccumulated = 0f;
                ApplyDamage(new DamageInfo(amount, Team.Player, _burnSourceId, Position, Vector2.zero, DamageKind.Burn));
            }
        }

        void TryContactDamage()
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive || player.IsIntangible)
                    continue;

                Vector2 toPlayer = player.Position - Position;
                float reach = Radius + player.Radius;
                if (toPlayer.sqrMagnitude > reach * reach)
                    continue;

                Vector2 direction = toPlayer.sqrMagnitude > 1e-6f ? toPlayer.normalized : Vector2.up;
                player.ApplyDamage(new DamageInfo(definition.ContactDamage * ContactDamageMultiplier, Team, EntityId, Position + direction * Radius, direction, DamageKind.Contact));

                // Bounce off so a swarm doesn't drain the shield in consecutive ticks.
                Velocity = -direction * MoveSpeed * 1.5f;
                _contactCooldown = ContactCooldown;
                _contactThisTick = true;
                return;
            }
        }

        /// <summary>Push away from overlapping allies so swarms spread out instead of stacking into one point.</summary>
        Vector2 Separation()
        {
            Vector2 push = Vector2.zero;
            foreach (var other in CombatRegistry.Enemies)
            {
                if (other == this || !other.IsAlive)
                    continue;

                Vector2 away = Position - other.Position;
                float minDistance = Radius + other.Radius + SeparationPadding;
                float distanceSquared = away.sqrMagnitude;
                if (distanceSquared >= minDistance * minDistance)
                    continue;

                float distance = Mathf.Sqrt(distanceSquared);
                Vector2 direction = distance > 1e-4f ? away / distance : PlaneMath.FromHeading(EntityId * 137.5f);
                push += direction * (1f - distance / minDistance);
            }
            return push;
        }
    }

    /// <summary>Decides where an enemy wants to go and what it does each tick.</summary>
    public abstract class EnemyBrain : MonoBehaviour
    {
        /// <summary>Called when the enemy is (re)initialised. Use <see cref="Enemy.Random"/> for any variation.</summary>
        public virtual void ResetState(Enemy self) { }

        /// <summary>Returns the desired velocity for this tick.</summary>
        public abstract Vector2 Think(Enemy self, float deltaTime, CombatWorld world);

        protected static PlayerShip Target(Enemy self) => CombatRegistry.NearestLivingPlayer(self.Position);
    }

    /// <summary>A brain that changes the damage its enemy takes, e.g. directional armour.</summary>
    public interface IDamageModifier
    {
        float IncomingDamageMultiplier(Enemy self, in DamageInfo info);
    }
}
