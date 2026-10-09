using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// All projectiles in the arena, simulated as plain structs on the fixed tick. Each projectile
    /// sweeps from its previous to its next position against the opposing team, so fast shots can't
    /// tunnel. Visuals are pooled GameObjects that only follow the simulation.
    /// </summary>
    public sealed class ProjectileSystem
    {
        /// <summary>World height that projectile visuals fly at.</summary>
        public const float VisualHeight = 0.5f;

        struct Projectile
        {
            public Vector2 Position;
            public Vector2 Previous;
            public Vector2 Velocity;
            public float RangeLeft;
            public float Damage;
            public float Radius;
            public Team Team;
            public int OwnerId;
            public GameObject VisualPrefab;
            public GameObject ImpactPrefab;
            public Transform Visual;
        }

        readonly List<Projectile> _active = new List<Projectile>(256);
        readonly Dictionary<GameObject, Stack<Transform>> _pools = new Dictionary<GameObject, Stack<Transform>>();
        readonly Transform _visualRoot;

        public int Count => _active.Count;

        public ProjectileSystem(Transform visualRoot) => _visualRoot = visualRoot;

        public void Spawn(in ProjectileSpawn spawn)
        {
            var projectile = new Projectile
            {
                Position = spawn.Position,
                Previous = spawn.Position,
                Velocity = spawn.Direction.normalized * spawn.Speed,
                RangeLeft = spawn.Range,
                Damage = spawn.Damage,
                Radius = spawn.Radius,
                Team = spawn.Team,
                OwnerId = spawn.OwnerId,
                VisualPrefab = spawn.VisualPrefab,
                ImpactPrefab = spawn.ImpactPrefab,
            };

            if (projectile.VisualPrefab != null)
            {
                projectile.Visual = Rent(projectile.VisualPrefab);
                projectile.Visual.SetPositionAndRotation(
                    PlaneMath.ToWorld(projectile.Position, VisualHeight),
                    Quaternion.LookRotation(PlaneMath.ToWorld(projectile.Velocity)));
            }

            _active.Add(projectile);
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
                Vector2 next = projectile.Position + projectile.Velocity * deltaTime;

                if (TryFindHit(projectile, next, out var target, out float t))
                {
                    Vector2 hitPoint = Vector2.Lerp(projectile.Position, next, t);
                    target.ApplyDamage(new DamageInfo(
                        projectile.Damage, projectile.Team, projectile.OwnerId, hitPoint, projectile.Velocity.normalized));

                    projectile.Position = hitPoint;
                    if (projectile.ImpactPrefab != null)
                        Object.Instantiate(projectile.ImpactPrefab, PlaneMath.ToWorld(hitPoint, VisualHeight), Quaternion.identity);

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
                if (projectile.Visual != null)
                    projectile.Visual.position = PlaneMath.ToWorld(Vector2.Lerp(projectile.Previous, projectile.Position, alpha), VisualHeight);
            }
        }

        static bool TryFindHit(in Projectile projectile, Vector2 next, out CombatEntity target, out float hitT)
        {
            target = null;
            hitT = float.MaxValue;

            foreach (var entity in CombatRegistry.OnTeam(projectile.Team.Opponent()))
            {
                if (!entity.IsAlive || entity.IsIntangible)
                    continue;

                if (PlaneMath.SegmentHitsCircle(projectile.Position, next, entity.Position, entity.Radius + projectile.Radius, out float t) && t < hitT)
                {
                    hitT = t;
                    target = entity;
                }
            }
            return target != null;
        }

        void RemoveAt(int index, in Projectile projectile)
        {
            if (projectile.Visual != null)
                Return(projectile.VisualPrefab, projectile.Visual);

            int last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }

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

        public ProjectileSpawn(Vector2 position, Vector2 direction, float speed, float damage, float radius, float range,
            Team team, int ownerId, GameObject visualPrefab, GameObject impactPrefab = null)
        {
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
        }
    }
}
