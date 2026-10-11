using System;
using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>One galaxy's arena hazard (M7): hazard enemies to place, and drifting spore clouds.</summary>
    [Serializable]
    public struct HazardSetup
    {
        [Tooltip("A hazard 'enemy' (asteroid, laser pylon), or none.")] public EnemyDefinition Enemy;
        [Min(0)] public int Count;
        [Tooltip("Replace one that's destroyed after a few seconds (asteroids), or not (pylons).")] public bool Respawn;
        [Min(0), Tooltip("Spore clouds drifting through the arena.")] public int Clouds;
        public GameObject CloudPrefab;
    }

    /// <summary>
    /// Puts each galaxy's hazard in its combat arenas (M7, GDD §2): drifting asteroids in the Shattered Expanse, sweeping
    /// laser pylons in the Graveyard Drift, drifting spore clouds in the Veil Nebula. Hazard enemies are ordinary enemies
    /// flagged as hazards, so they never hold up a level's waves. Spore clouds are plain floor hazards that drift, bounce
    /// off the walls, and slow and burn whoever is inside. Placement is seeded and kept clear of the pilots. Authority only,
    /// on the fixed tick.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class HazardDirector : MonoBehaviour
    {
        const float RespawnDelay = 5f;
        const float ClearOfPilots = 7f;

        [SerializeField] CombatWorld world;
        [SerializeField, Min(0.5f)] float cloudRadius = 2.6f;
        [SerializeField, Min(0f)] float cloudSpeed = 1.2f;
        [SerializeField, Range(0.1f, 1f)] float cloudSlow = 0.6f;
        [SerializeField, Min(0f)] float cloudBurn = 4f;
        [SerializeField, Min(0.1f)] float cloudBurnInterval = 0.6f;

        sealed class Cloud
        {
            public Vector2 Position, Velocity;
            public GameObject View;
        }

        readonly List<Enemy> _hazards = new List<Enemy>();
        readonly List<Cloud> _clouds = new List<Cloud>();
        readonly List<float> _respawns = new List<float>();
        HazardSetup _setup;
        IRandom _random;
        float _burnTimer, _age;
        bool _running;

        public bool IsRunning => _running;
        public IReadOnlyList<Enemy> Hazards => _hazards;
        public int CloudCount => _clouds.Count;

        /// <summary>The spore clouds' centres, for tests.</summary>
        public IEnumerable<Vector2> CloudPositions
        {
            get
            {
                foreach (var cloud in _clouds)
                    yield return cloud.Position;
            }
        }

        public float CloudRadius => cloudRadius;

        public void Begin(HazardSetup setup, Seed seed)
        {
            Stop();
            _setup = setup;
            _random = seed.CreateRandom("hazards");
            _running = true;
            _age = 0f;
            if (setup.Enemy != null)
            {
                for (int i = 0; i < setup.Count; i++)
                    SpawnHazard(edge: false);
            }
            for (int i = 0; i < setup.Clouds; i++)
                SpawnCloud();
        }

        /// <summary>Removes every hazard. Hazard enemies are despawned, not killed: no drops.</summary>
        public void Stop()
        {
            _running = false;
            foreach (var hazard in _hazards)
            {
                if (hazard != null && hazard.IsAlive)
                    hazard.Despawn();
            }
            _hazards.Clear();
            _respawns.Clear();
            foreach (var cloud in _clouds)
            {
                if (cloud.View != null)
                    Destroy(cloud.View);
            }
            _clouds.Clear();
        }

        void FixedUpdate()
        {
            if (!_running || GameClock.Paused || !Authority.IsAuthority || world.IsOver)
                return;

            float deltaTime = Time.fixedDeltaTime;
            _age += deltaTime;
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                if (_hazards[i] != null && _hazards[i].IsAlive)
                    continue;
                _hazards.RemoveAt(i);
                if (_setup.Respawn)
                    _respawns.Add(RespawnDelay);
            }
            for (int i = _respawns.Count - 1; i >= 0; i--)
            {
                _respawns[i] -= deltaTime;
                if (_respawns[i] > 0f)
                    continue;
                _respawns.RemoveAt(i);
                SpawnHazard(edge: true);
            }
            UpdateClouds(deltaTime);
        }

        void SpawnHazard(bool edge)
        {
            Vector2 position = edge ? EdgePoint() : ClearPoint(_setup.Enemy.Prefab.Radius + 1f);
            var hazard = world.SpawnEnemy(_setup.Enemy, position, dropsLoot: true);
            _hazards.Add(hazard);
        }

        void SpawnCloud()
        {
            var cloud = new Cloud
            {
                Position = ClearPoint(cloudRadius + 1f),
                Velocity = PlaneMath.FromHeading(_random.Range(0f, 360f)) * cloudSpeed,
            };
            if (_setup.CloudPrefab != null)
            {
                cloud.View = Instantiate(_setup.CloudPrefab, PlaneMath.ToWorld(cloud.Position), Quaternion.identity, transform);
                if (cloud.View.TryGetComponent(out SporeCloud view))
                    view.Begin(cloudRadius);
            }
            _clouds.Add(cloud);
        }

        void UpdateClouds(float deltaTime)
        {
            if (_clouds.Count == 0)
                return;
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(cloudRadius, cloudRadius);
            foreach (var cloud in _clouds)
            {
                cloud.Position += cloud.Velocity * deltaTime;
                if (Mathf.Abs(cloud.Position.x) > half.x)
                    cloud.Velocity.x = -Mathf.Sign(cloud.Position.x) * Mathf.Abs(cloud.Velocity.x);
                if (Mathf.Abs(cloud.Position.y) > half.y)
                    cloud.Velocity.y = -Mathf.Sign(cloud.Position.y) * Mathf.Abs(cloud.Velocity.y);
                cloud.Position = new Vector2(Mathf.Clamp(cloud.Position.x, -half.x, half.x), Mathf.Clamp(cloud.Position.y, -half.y, half.y));
                if (cloud.View != null)
                {
                    cloud.View.transform.position = PlaneMath.ToWorld(cloud.Position);
                    if (cloud.View.TryGetComponent(out SporeCloud view))
                        view.SetLife(_age, float.MaxValue);
                }
            }

            _burnTimer -= deltaTime;
            bool burn = _burnTimer <= 0f;
            if (burn)
                _burnTimer = cloudBurnInterval;
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive)
                    continue;
                foreach (var cloud in _clouds)
                {
                    float reach = cloudRadius + player.Radius;
                    if ((player.Position - cloud.Position).sqrMagnitude > reach * reach)
                        continue;
                    player.Slow(cloudSlow, 0.15f);
                    if (burn)
                    {
                        Vector2 away = player.Position - cloud.Position;
                        player.ApplyDamage(new DamageInfo(cloudBurn, Team.Enemy, 0, player.Position,
                            away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up, DamageKind.Contact));
                    }
                    break;
                }
            }
        }

        /// <summary>A seeded point in the arena, clear of every pilot and away from the walls.</summary>
        Vector2 ClearPoint(float margin)
        {
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(margin, margin);
            Vector2 candidate = Vector2.zero;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                candidate = new Vector2(_random.Range(-half.x, half.x), _random.Range(-half.y, half.y));
                if (ClearOfAllPilots(candidate))
                    break;
            }
            return candidate;
        }

        /// <summary>A seeded point just inside an arena edge, clear of the pilots: where a replacement asteroid drifts in.</summary>
        Vector2 EdgePoint()
        {
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(1.5f, 1.5f);
            Vector2 candidate = Vector2.zero;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                bool vertical = _random.Range(0, 2) == 0;
                float side = _random.Range(0, 2) == 0 ? -1f : 1f;
                candidate = vertical ? new Vector2(side * half.x, _random.Range(-half.y, half.y)) : new Vector2(_random.Range(-half.x, half.x), side * half.y);
                if (ClearOfAllPilots(candidate))
                    break;
            }
            return candidate;
        }

        static bool ClearOfAllPilots(Vector2 point)
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive && (player.Position - point).sqrMagnitude < ClearOfPilots * ClearOfPilots)
                    return false;
            }
            return true;
        }

        void OnDestroy() => Stop();
    }
}
