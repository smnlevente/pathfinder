using System.Collections.Generic;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// M1 test spawner: endless escalating waves of Drones and Gunners, so combat feel can be judged. M2
    /// replaces it with the budget-based wave director (GDD §7.2). Spawns are telegraphed and never
    /// land near a player.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class WaveSpawner : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField] EnemyDefinition drone;
        [SerializeField] EnemyDefinition gunner;
        [SerializeField] GameObject telegraphPrefab;

        [Header("Timing")]
        [SerializeField, Min(0f)] float firstWaveDelay = 1.5f;
        [SerializeField, Min(0f)] float telegraphDuration = 1f;
        [SerializeField, Min(1f)] float waveTimeout = 25f;
        [SerializeField, Min(0), Tooltip("The next wave starts when at most this many enemies remain.")] int nextWaveThreshold = 2;

        [Header("Placement")]
        [SerializeField, Min(0f)] float minPlayerDistance = 6f;
        [SerializeField, Min(0f)] float arenaInset = 3f;

        struct PendingSpawn
        {
            public EnemyDefinition Definition;
            public Vector2 Position;
            public float TimeLeft;
            public GameObject Telegraph;
        }

        readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
        IRandom _random;
        float _waveTimer;
        int _spawnCount;

        public int Wave { get; private set; }
        public EnemyDefinition DroneDefinition => drone;
        public EnemyDefinition GunnerDefinition => gunner;

        void Start()
        {
            _random = world.Seed.Derive(SeedLabels.Map).CreateRandom(SeedLabels.Waves);
            _waveTimer = waveTimeout - firstWaveDelay;
        }

        void FixedUpdate()
        {
            if (GameClock.Paused || !Authority.IsAuthority || world.IsOver)
                return;

            float deltaTime = Time.fixedDeltaTime;
            AdvancePending(deltaTime);

            _waveTimer += deltaTime;
            bool cleared = Wave > 0 && CombatRegistry.Enemies.Count + _pending.Count <= nextWaveThreshold;
            if (_waveTimer >= waveTimeout || cleared)
                StartNextWave();
        }

        void StartNextWave()
        {
            Wave++;
            _waveTimer = 0f;

            int drones = Mathf.Min(4 + Wave, 14);
            int gunners = Mathf.Min(Wave / 2, 5);

            for (int i = 0; i < drones; i++)
                QueueSpawn(drone);
            for (int i = 0; i < gunners; i++)
                QueueSpawn(gunner);
        }

        void QueueSpawn(EnemyDefinition definition)
        {
            Vector2 position = PickSpawnPoint();
            _pending.Add(new PendingSpawn
            {
                Definition = definition,
                Position = position,
                TimeLeft = telegraphDuration,
                Telegraph = telegraphPrefab != null
                    ? Instantiate(telegraphPrefab, PlaneMath.ToWorld(position), Quaternion.identity, transform)
                    : null,
            });
        }

        void AdvancePending(float deltaTime)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var pending = _pending[i];
                pending.TimeLeft -= deltaTime;
                if (pending.TimeLeft > 0f)
                {
                    _pending[i] = pending;
                    continue;
                }

                if (pending.Telegraph != null)
                    Destroy(pending.Telegraph);
                Spawn(pending.Definition, pending.Position);
                _pending.RemoveAt(i--);
            }
        }

        /// <summary>Spawns an enemy immediately, with no telegraph. Also used by tests and debug tools.</summary>
        public Enemy Spawn(EnemyDefinition definition, Vector2 position)
        {
            var enemy = Instantiate(definition.Prefab, PlaneMath.ToWorld(position), Quaternion.identity);
            enemy.Initialize(definition, world.Seed.Derive(SeedLabels.Enemy).Derive(_spawnCount++).CreateRandom(SeedLabels.Ai));
            return enemy;
        }

        Vector2 PickSpawnPoint()
        {
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(arenaInset, arenaInset);
            Vector2 candidate = Vector2.zero;

            // Retry a few times to stay clear of every player. Fall back to the last candidate in a crowded arena.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                candidate = new Vector2(_random.Range(-half.x, half.x), _random.Range(-half.y, half.y));
                if (IsClearOfPlayers(candidate))
                    break;
            }
            return candidate;
        }

        bool IsClearOfPlayers(Vector2 point)
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive && (player.Position - point).sqrMagnitude < minPlayerDistance * minPlayerDistance)
                    return false;
            }
            return true;
        }
    }
}
