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
    /// Shared enemy body: health, steering, separation, contact damage and death. Behaviour comes from the
    /// <see cref="EnemyBrain"/> on the same GameObject.
    /// </summary>
    public sealed class Enemy : CombatEntity
    {
        const float ContactCooldown = 0.75f;
        const float SeparationPadding = 0.3f;

        [SerializeField] EnemyDefinition definition;
        [SerializeField, Min(0.01f), Tooltip("Seconds to reach full speed.")] float accelerationTime = 0.25f;

        public override Team Team => Team.Enemy;
        public override Vitals Vitals => _vitals;

        public EnemyDefinition Definition => definition;
        public Vector2 Velocity { get; private set; }

        /// <summary>Deterministic per-enemy randomness for its brain (GDD §14.3).</summary>
        public IRandom Random { get; private set; }

        /// <summary>Presentation hint: the enemy is winding up an attack.</summary>
        public bool IsTelegraphing { get; set; }

        Vitals _vitals;
        EnemyBrain _brain;
        float _contactCooldown;
        bool _headingOverridden;

        protected override void Awake()
        {
            base.Awake();
            _brain = GetComponent<EnemyBrain>();
            if (_brain == null)
                Debug.LogError($"[Enemy] {name} needs an {nameof(EnemyBrain)} component.", this);
            if (definition != null)
                Initialize(definition, new Pcg32(0UL, 0UL));
        }

        public void Initialize(EnemyDefinition enemyDefinition, IRandom random)
        {
            definition = enemyDefinition;
            Random = random;
            _vitals = new Vitals(definition.MaxHealth);
            Velocity = Vector2.zero;
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

        public override void Simulate(float deltaTime, CombatWorld world)
        {
            _headingOverridden = false;
            Vector2 desired = _brain.Think(this, deltaTime, world);
            desired += Separation() * definition.MoveSpeed;

            float acceleration = definition.MoveSpeed / accelerationTime;
            Velocity = Vector2.MoveTowards(Velocity, Vector2.ClampMagnitude(desired, definition.MoveSpeed), acceleration * deltaTime);
            Position = world.ClampToArena(Position + Velocity * deltaTime, Radius);

            if (!_headingOverridden && Velocity.sqrMagnitude > 0.01f)
                Heading = PlaneMath.HeadingDegrees(Velocity);

            _contactCooldown -= deltaTime;
            if (_contactCooldown <= 0f && definition.ContactDamage > 0f)
                TryContactDamage();

            _vitals.Tick(deltaTime);
        }

        protected override void OnKilled(in DamageInfo info) => Destroy(gameObject);

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
                player.ApplyDamage(new DamageInfo(definition.ContactDamage, Team, EntityId, Position + direction * Radius, direction));

                // Bounce off so a swarm doesn't drain the shield in consecutive ticks.
                Velocity = -direction * definition.MoveSpeed * 1.5f;
                _contactCooldown = ContactCooldown;
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
}
