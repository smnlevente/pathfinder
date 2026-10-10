using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// All projectiles in the arena, simulated as plain structs on the fixed tick. Each projectile
    /// sweeps from its previous to its next position against the opposing team, so fast shots can't
    /// tunnel. Projectiles can pierce, home in on targets, and lose damage over range. Visuals are
    /// pooled GameObjects that only follow the simulation.
    /// </summary>
    public sealed class ProjectileSystem
    {
        /// <summary>World height that projectile visuals fly at.</summary>
        public const float VisualHeight = 0.5f;

        const float RetargetRange = 25f;

        struct Projectile
        {
            public Vector2 Position;
            public Vector2 Previous;
            public Vector2 Velocity;
            public float Range;
            public float RangeLeft;
            public float Damage;
            public float DamageAtMaxRange;
            public float Radius;
            public Team Team;
            public int OwnerId;
            public int PierceLeft;
            public HashSet<int> HitIds;
            public float TurnRate;
            public int TargetId;
            public DamageKind Kind;
            public bool Phased;
            public float FadeLeft; // > 0: defused, harmless and fading out
            public Vector3 VisualScale;
            public GameObject VisualPrefab;
            public GameObject ImpactPrefab;
            public Transform Visual;
        }

        readonly List<Projectile> _active = new List<Projectile>(256);
        readonly Dictionary<GameObject, Stack<Transform>> _pools = new Dictionary<GameObject, Stack<Transform>>();
        readonly Stack<HashSet<int>> _hitSetPool = new Stack<HashSet<int>>();
        readonly List<TrailRenderer> _trailBuffer = new List<TrailRenderer>();
        readonly Transform _visualRoot;

        const float DefuseTime = 0.5f;

        /// <summary>
        /// Weak points sit on top of a boss's armoured body, so a shot that clips the shell this close to one counts as
        /// hitting it instead of deflecting.
        /// </summary>
        const float ArmourLeniency = 0.8f;

        public int Count => _active.Count;

        /// <summary>Spark shown when a projectile is stopped by armour.</summary>
        public GameObject DeflectPrefab { get; set; }

        public ProjectileSystem(Transform visualRoot) => _visualRoot = visualRoot;

        public int CountForTeam(Team team)
        {
            int count = 0;
            foreach (var projectile in _active)
            {
                if (projectile.Team == team)
                    count++;
            }
            return count;
        }

        public void Spawn(in ProjectileSpawn spawn)
        {
            var projectile = new Projectile
            {
                Position = spawn.Position,
                Previous = spawn.Position,
                Velocity = spawn.Direction.normalized * spawn.Speed,
                Range = spawn.Range,
                RangeLeft = spawn.Range,
                Damage = spawn.Damage,
                DamageAtMaxRange = spawn.DamageAtMaxRange,
                Radius = spawn.Radius,
                Team = spawn.Team,
                OwnerId = spawn.OwnerId,
                PierceLeft = spawn.Pierce,
                HitIds = spawn.Pierce > 0 ? RentHitSet() : null,
                TurnRate = spawn.TurnRate,
                TargetId = spawn.TargetId,
                Kind = spawn.Kind,
                VisualPrefab = spawn.VisualPrefab,
                ImpactPrefab = spawn.ImpactPrefab,
            };

            if (projectile.VisualPrefab != null)
            {
                var visual = Rent(projectile.VisualPrefab);
                visual.SetPositionAndRotation(
                    PlaneMath.ToWorld(projectile.Position, VisualHeight),
                    Quaternion.LookRotation(PlaneMath.ToWorld(projectile.Velocity)));
                visual.localScale = projectile.VisualPrefab.transform.localScale * spawn.VisualScale;
                projectile.VisualScale = visual.localScale;

                // A reused visual must not draw a trail from wherever it last flew.
                visual.GetComponentsInChildren(_trailBuffer);
                foreach (var trail in _trailBuffer)
                    trail.Clear();

                projectile.Visual = visual;
            }

            _active.Add(projectile);
            CombatEvents.RaiseProjectileFired(projectile.Team, projectile.Position);
        }

        /// <summary>Removes every projectile, e.g. between levels.</summary>
        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                RemoveAt(i, _active[i]);
        }

        /// <summary>Removes the team's projectiles inside the circle, e.g. Nova Pulse erasing enemy bullets.</summary>
        public int DestroyInRadius(Team team, Vector2 center, float radius)
        {
            int destroyed = 0;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var projectile = _active[i];
                float reach = radius + projectile.Radius;
                if (projectile.Team == team && (projectile.Position - center).sqrMagnitude <= reach * reach)
                {
                    RemoveAt(i, projectile);
                    destroyed++;
                }
            }
            return destroyed;
        }

        /// <summary>
        /// Makes the team's projectiles harmless: they slow, shrink and vanish over half a second. Used when a boss dies,
        /// so a stray bullet can't kill a pilot who has just won.
        /// </summary>
        public void Defuse(Team team)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var projectile = _active[i];
                if (projectile.Team != team || projectile.FadeLeft > 0f)
                    continue;
                projectile.FadeLeft = DefuseTime;
                projectile.Velocity *= 0.3f;
                projectile.TurnRate = 0f;
                _active[i] = projectile;
            }
        }

        internal void CapturePrevious()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var projectile = _active[i];
                projectile.Previous = projectile.Position;
                _active[i] = projectile;
            }
        }

        internal void Simulate(float deltaTime, CombatWorld world)
        {
            // Iterate backwards so swap-removal only moves already-processed projectiles.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var projectile = _active[i];
                if (projectile.FadeLeft > 0f)
                {
                    projectile.FadeLeft -= deltaTime;
                    projectile.Position += projectile.Velocity * deltaTime;
                    if (projectile.FadeLeft <= 0f)
                        RemoveAt(i, projectile);
                    else
                        _active[i] = projectile;
                    continue;
                }

                if (projectile.TurnRate > 0f)
                    Steer(ref projectile, deltaTime);

                Vector2 next = projectile.Position + projectile.Velocity * deltaTime;
                bool consumed = false;

                if (!projectile.Phased)
                    NotifyPhasing(ref projectile, next);

                // A piercing projectile can pass through several targets in one tick, in order along its path.
                while (TryFindHit(projectile, next, out var target, out float t))
                {
                    Vector2 hitPoint = Vector2.Lerp(projectile.Position, next, t);
                    if (target.Armoured)
                    {
                        // Armour stops every shot, piercing or not, without damage or on-hit effects.
                        if (DeflectPrefab != null)
                            Object.Instantiate(DeflectPrefab, PlaneMath.ToWorld(hitPoint, VisualHeight), Quaternion.identity);
                        CombatEvents.RaiseDeflected(hitPoint);
                        projectile.Position = hitPoint;
                        consumed = true;
                        break;
                    }

                    float travelled = projectile.Range - projectile.RangeLeft + (hitPoint - projectile.Position).magnitude;
                    float falloff = Mathf.Lerp(1f, projectile.DamageAtMaxRange, Mathf.Clamp01(travelled / projectile.Range));

                    target.ApplyDamage(new DamageInfo(projectile.Damage * falloff, projectile.Team, projectile.OwnerId, hitPoint, projectile.Velocity.normalized, projectile.Kind));
                    if (projectile.ImpactPrefab != null)
                        Object.Instantiate(projectile.ImpactPrefab, PlaneMath.ToWorld(hitPoint, VisualHeight), Quaternion.identity);

                    if (projectile.PierceLeft <= 0)
                    {
                        projectile.Position = hitPoint;
                        consumed = true;
                        break;
                    }

                    projectile.PierceLeft--;
                    projectile.HitIds.Add(target.EntityId);
                }

                if (consumed)
                {
                    RemoveAt(i, projectile);
                    continue;
                }

                projectile.RangeLeft -= projectile.Velocity.magnitude * deltaTime;
                projectile.Position = next;

                if (projectile.RangeLeft <= 0f || !world.IsInsideArena(next, margin: 1f))
                {
                    RemoveAt(i, projectile);
                    continue;
                }

                _active[i] = projectile;
            }
        }

        internal void UpdateVisuals(float alpha)
        {
            foreach (var projectile in _active)
            {
                if (projectile.Visual == null)
                    continue;

                projectile.Visual.position = PlaneMath.ToWorld(Vector2.Lerp(projectile.Previous, projectile.Position, alpha), VisualHeight);
                if (projectile.FadeLeft > 0f)
                    projectile.Visual.localScale = projectile.VisualScale * (projectile.FadeLeft / DefuseTime);
                if (projectile.TurnRate > 0f && projectile.Velocity.sqrMagnitude > 1e-6f)
                    projectile.Visual.rotation = Quaternion.LookRotation(PlaneMath.ToWorld(projectile.Velocity));
            }
        }

        /// <summary>Turns a homing projectile toward its target, picking the nearest one if it has none.</summary>
        static void Steer(ref Projectile projectile, float deltaTime)
        {
            var target = FindLivingTarget(projectile);
            if (target == null)
            {
                target = NearestOpponent(projectile);
                projectile.TargetId = target != null ? target.EntityId : 0;
            }
            if (target == null)
                return;

            Vector2 desired = target.Position - projectile.Position;
            if (desired.sqrMagnitude < 1e-6f)
                return;

            float angle = Vector2.SignedAngle(projectile.Velocity, desired);
            float maxTurn = projectile.TurnRate * deltaTime;
            projectile.Velocity = PlaneMath.Rotate(projectile.Velocity, Mathf.Clamp(angle, -maxTurn, maxTurn));
        }

        static CombatEntity FindLivingTarget(in Projectile projectile)
        {
            if (projectile.TargetId == 0)
                return null;

            foreach (var entity in CombatRegistry.OnTeam(projectile.Team.Opponent()))
            {
                if (entity.EntityId == projectile.TargetId)
                    return entity.IsAlive && !entity.Armoured && !entity.IsIntangible ? entity : null;
            }
            return null;
        }

        static CombatEntity NearestOpponent(in Projectile projectile)
        {
            CombatEntity nearest = null;
            float best = RetargetRange * RetargetRange;
            foreach (var entity in CombatRegistry.OnTeam(projectile.Team.Opponent()))
            {
                float distance = (entity.Position - projectile.Position).sqrMagnitude;
                if (entity.IsAlive && !entity.IsIntangible && !entity.Armoured && distance < best)
                {
                    best = distance;
                    nearest = entity;
                }
            }
            return nearest;
        }

        /// <summary>Tells an intangible (dashing) pilot that a shot passed through it, once per projectile. Used by Phase Dash.</summary>
        static void NotifyPhasing(ref Projectile projectile, Vector2 next)
        {
            foreach (var entity in CombatRegistry.OnTeam(projectile.Team.Opponent()))
            {
                if (entity.IsAlive && entity.IsIntangible && entity is Player.PlayerShip ship
                    && PlaneMath.SegmentHitsCircle(projectile.Position, next, entity.Position, entity.Radius + projectile.Radius, out _))
                {
                    projectile.Phased = true;
                    ship.NotifyPhasedThrough();
                    return;
                }
            }
        }

        static bool TryFindHit(in Projectile projectile, Vector2 next, out CombatEntity target, out float hitT)
        {
            target = null;
            hitT = float.MaxValue;

            foreach (var entity in CombatRegistry.OnTeam(projectile.Team.Opponent()))
            {
                if (CanHit(projectile, entity)
                    && PlaneMath.SegmentHitsCircle(projectile.Position, next, entity.Position, entity.Radius + projectile.Radius, out float t) && t < hitT)
                {
                    hitT = t;
                    target = entity;
                }
            }

            if (target != null && target.Armoured)
                PreferNearbyWeakPoint(projectile, next, ref target, ref hitT);
            return target != null;
        }

        /// <summary>Homing shots are guided munitions: they fly over armour to reach what they're homing on.</summary>
        static bool CanHit(in Projectile projectile, CombatEntity entity) =>
            entity.IsAlive && !entity.IsIntangible && !(entity.Armoured && projectile.TurnRate > 0f)
            && (projectile.HitIds == null || !projectile.HitIds.Contains(entity.EntityId));

        /// <summary>The shot hit armour first: if its path reaches an unarmoured part within the leniency, that part takes it.</summary>
        static void PreferNearbyWeakPoint(in Projectile projectile, Vector2 next, ref CombatEntity target, ref float hitT)
        {
            Vector2 path = next - projectile.Position;
            float length = path.magnitude;
            if (length < 1e-5f)
                return;

            Vector2 reachEnd = next + path / length * ArmourLeniency;
            float reachLength = length + ArmourLeniency;
            float limit = hitT * length + ArmourLeniency; // distance along the path
            foreach (var entity in CombatRegistry.OnTeam(projectile.Team.Opponent()))
            {
                if (entity.Armoured || !CanHit(projectile, entity))
                    continue;
                if (PlaneMath.SegmentHitsCircle(projectile.Position, reachEnd, entity.Position, entity.Radius + projectile.Radius, out float t)
                    && t * reachLength <= limit)
                {
                    limit = t * reachLength;
                    target = entity;
                    hitT = t * reachLength / length;
                }
            }
        }

        void RemoveAt(int index, in Projectile projectile)
        {
            if (projectile.Visual != null)
                Return(projectile.VisualPrefab, projectile.Visual);
            if (projectile.HitIds != null)
            {
                projectile.HitIds.Clear();
                _hitSetPool.Push(projectile.HitIds);
            }

            int last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }

        HashSet<int> RentHitSet() => _hitSetPool.Count > 0 ? _hitSetPool.Pop() : new HashSet<int>();

        Transform Rent(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out var pool) && pool.Count > 0)
            {
                var pooled = pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            return Object.Instantiate(prefab, _visualRoot).transform;
        }

        void Return(GameObject prefab, Transform visual)
        {
            visual.gameObject.SetActive(false);
            if (!_pools.TryGetValue(prefab, out var pool))
                _pools[prefab] = pool = new Stack<Transform>();
            pool.Push(visual);
        }
    }

    public readonly struct ProjectileSpawn
    {
        public Vector2 Position { get; }
        public Vector2 Direction { get; }
        public float Speed { get; }
        public float Damage { get; }
        public float Radius { get; }
        public float Range { get; }
        public Team Team { get; }
        public int OwnerId { get; }
        public GameObject VisualPrefab { get; }
        public GameObject ImpactPrefab { get; }

        /// <summary>Extra targets the projectile passes through before it is consumed.</summary>
        public int Pierce { get; }

        /// <summary>Damage multiplier reached at maximum range (1 = no falloff).</summary>
        public float DamageAtMaxRange { get; }

        /// <summary>Homing turn rate in degrees per second (0 = flies straight).</summary>
        public float TurnRate { get; }

        /// <summary>Preferred homing target. 0 = nearest.</summary>
        public int TargetId { get; }

        public float VisualScale { get; }

        /// <summary>The damage kind dealt on hit. Only primary-weapon projectiles trigger on-hit effects.</summary>
        public DamageKind Kind { get; }

        public ProjectileSpawn(Vector2 position, Vector2 direction, float speed, float damage, float radius, float range,
            Team team, int ownerId, GameObject visualPrefab, GameObject impactPrefab = null,
            int pierce = 0, float damageAtMaxRange = 1f, float turnRate = 0f, int targetId = 0, float visualScale = 1f,
            DamageKind kind = DamageKind.Projectile)
        {
            Kind = kind;
            Position = position;
            Direction = direction;
            Speed = speed;
            Damage = damage;
            Radius = radius;
            Range = range;
            Team = team;
            OwnerId = ownerId;
            VisualPrefab = visualPrefab;
            ImpactPrefab = impactPrefab;
            Pierce = pierce;
            DamageAtMaxRange = damageAtMaxRange;
            TurnRate = turnRate;
            TargetId = targetId;
            VisualScale = visualScale;
        }
    }
}
