using System;
using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Budget-driven wave spawning (GDD §7.2). Each wave spends a budget, scaled by galaxy, map row and wave
    /// number, on a weighted mix of the theme's enemies, some of which may roll as elites. The next wave starts
    /// when most of the current wave is dead or a timeout passes. Spawns are telegraphed and never land on a
    /// player. Authority only.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class WaveDirector : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField] WaveDirectorConfig config;
        [SerializeField] GameObject telegraphPrefab;

        [Header("Level")]
        [SerializeField, Min(0), Tooltip("0-based galaxy index (GDD §2).")] int galaxyIndex;
        [SerializeField, Min(1), Tooltip("Galaxy map row of this level (GDD §9).")] int row = 1;
        [SerializeField, Min(0), Tooltip("Number of waves in this level. 0 = endless (combat sandbox).")] int waveCount;

        struct PendingSpawn
        {
            public EnemyDefinition Definition;
            public EliteModifierDefinition Elite;
            public Vector2 Position;
            public float TimeLeft;
            public GameObject Telegraph;
        }

        readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
        readonly List<Enemy> _waveEnemies = new List<Enemy>();
        readonly List<WaveEntry> _entries = new List<WaveEntry>();
        IRandom _random;
        float _waveTimer;
        int _waveSize;

        public WaveDirectorConfig Config => config;
        public int Wave { get; private set; }
        public int CurrentBudget { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>Raised when a wave's spawns are queued.</summary>
        public event Action<WaveDirector> WaveStarted;

        /// <summary>Raised once, when the final wave of a finite level is cleared.</summary>
        public event Action<WaveDirector> Completed;

        void Start()
        {
            _random = world.Seed.Derive(SeedLabels.Level).Derive(galaxyIndex).Derive(row).CreateRandom(SeedLabels.Waves);
            foreach (var entry in config.Enemies)
                _entries.Add(new WaveEntry(entry.Enemy.BudgetCost, entry.Weight, Mathf.Max(1, entry.MinWave)));
            _waveTimer = config.WaveTimeout - config.FirstWaveDelay;
        }

        void FixedUpdate()
        {
            if (GameClock.Paused || !Authority.IsAuthority || world.IsOver || IsComplete)
                return;

            float deltaTime = Time.fixedDeltaTime;
            AdvancePending(deltaTime);
            _waveEnemies.RemoveAll(enemy => enemy == null || !enemy.IsAlive);

            bool finalWaveSpawned = waveCount > 0 && Wave >= waveCount;
            if (finalWaveSpawned)
            {
                if (_pending.Count == 0 && _waveEnemies.Count == 0)
                {
                    IsComplete = true;
                    Completed?.Invoke(this);
                }
                return;
            }

            _waveTimer += deltaTime;
            if (_waveTimer >= config.WaveTimeout || (Wave > 0 && IsWaveMostlyCleared()))
                StartNextWave();
        }

        /// <summary>Starts the next wave immediately. For debug tools.</summary>
        public void SkipToNextWave()
        {
            if (!IsComplete && (waveCount == 0 || Wave < waveCount))
                StartNextWave();
        }

        bool IsWaveMostlyCleared()
        {
            int alive = _waveEnemies.Count + _pending.Count;
            int allowed = Mathf.FloorToInt(_waveSize * (1f - config.ClearFraction));
            return alive <= allowed;
        }

        void StartNextWave()
        {
            Wave++;
            _waveTimer = 0f;
            CurrentBudget = config.Budget(Wave, galaxyIndex, row);
            float eliteChance = config.Elites.Length > 0 ? config.EliteChance(Wave, galaxyIndex, row) : 0f;

            var orders = WaveComposer.Compose(_entries, CurrentBudget, Wave, eliteChance, config.EliteCostMultiplier, _random);
            _waveSize = orders.Count;
            for (int i = 0; i < orders.Count; i++)
            {
                var elite = orders[i].Elite ? config.Elites[_random.Range(0, config.Elites.Length)] : null;
                QueueSpawn(config.Enemies[orders[i].EntryIndex].Enemy, elite, config.TelegraphDuration + i * config.SpawnStagger);
            }

            WaveStarted?.Invoke(this);
        }

        void QueueSpawn(EnemyDefinition definition, EliteModifierDefinition elite, float delay)
        {
            Vector2 position = PickSpawnPoint();
            _pending.Add(new PendingSpawn
            {
                Definition = definition,
                Elite = elite,
                Position = position,
                TimeLeft = delay,
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
                _waveEnemies.Add(world.SpawnEnemy(pending.Definition, pending.Position, pending.Elite));
                _pending.RemoveAt(i--);
            }
        }

        Vector2 PickSpawnPoint()
        {
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(config.ArenaInset, config.ArenaInset);
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
            float minDistance = config.MinPlayerDistance;
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive && (player.Position - point).sqrMagnitude < minDistance * minDistance)
                    return false;
            }
            return true;
        }
    }
}
