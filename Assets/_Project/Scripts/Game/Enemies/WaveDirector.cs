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
    /// The run's level controller reconfigures the director for each level. The combat sandbox lets it start
    /// itself in endless mode.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class WaveDirector : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField] WaveDirectorConfig config;
        [SerializeField] GameObject telegraphPrefab;

        [Header("Sandbox")]
        [SerializeField, Tooltip("Start by itself with the settings below. The run's level controller turns this off.")] bool autoStart = true;
        [SerializeField, Min(0), Tooltip("0-based galaxy index (GDD §2).")] int galaxyIndex;
        [SerializeField, Min(1), Tooltip("Galaxy map row (GDD §9).")] int row = 1;
        [SerializeField, Min(0), Tooltip("Number of waves. 0 = endless.")] int waveCount;

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
        bool _running;

        public WaveDirectorConfig Config => config;
        public int Wave { get; private set; }
        public int WaveCount => waveCount;
        public int CurrentBudget { get; private set; }
        public bool IsRunning => _running;
        public bool IsComplete { get; private set; }

        /// <summary>Multiplies every wave budget, e.g. ×1.3 in Scraps levels.</summary>
        public float BudgetMultiplier { get; set; } = 1f;

        /// <summary>Seconds before the next wave starts anyway. Lower values keep pressure constant.</summary>
        public float WaveTimeout { get; set; }

        /// <summary>Living enemies of the current wave, plus those still telegraphing.</summary>
        public int RemainingInWave => _waveEnemies.Count + _pending.Count;

        /// <summary>Raised when a wave's spawns are queued.</summary>
        public event Action<WaveDirector> WaveStarted;

        /// <summary>Raised once, when the final wave of a finite level is cleared.</summary>
        public event Action<WaveDirector> Completed;

        void Awake()
        {
            foreach (var entry in config.Enemies)
                _entries.Add(new WaveEntry(entry.Enemy.BudgetCost, entry.Weight, Mathf.Max(1, entry.MinWave)));
            WaveTimeout = config.WaveTimeout;
        }

        void Start()
        {
            if (autoStart && !_running)
                Begin(world.Seed.Derive(SeedLabels.Level).Derive(galaxyIndex).Derive(row), galaxyIndex, row, waveCount);
        }

        /// <summary>
        /// Starts spawning for a level. <paramref name="waves"/> = 0 is endless. The first wave arrives after
        /// <paramref name="firstWaveDelay"/> seconds (config default when negative).
        /// </summary>
        public void Begin(Seed levelSeed, int galaxy, int mapRow, int waves, float budgetMultiplier = 1f, float firstWaveDelay = -1f)
        {
            Stop();
            _random = levelSeed.CreateRandom(SeedLabels.Waves);
            galaxyIndex = galaxy;
            row = mapRow;
            waveCount = waves;
            BudgetMultiplier = budgetMultiplier;
            WaveTimeout = config.WaveTimeout;
            Wave = 0;
            IsComplete = false;
            _waveSize = 0;
            _waveTimer = WaveTimeout - (firstWaveDelay >= 0f ? firstWaveDelay : config.FirstWaveDelay);
            _running = true;
        }

        /// <summary>Stops spawning and cancels pending telegraphs. Enemies already spawned are left alone.</summary>
        public void Stop()
        {
            foreach (var pending in _pending)
            {
                if (pending.Telegraph != null)
                    Destroy(pending.Telegraph);
            }
            _pending.Clear();
            _waveEnemies.Clear();
            _running = false;
        }

        void FixedUpdate()
        {
            if (!_running || GameClock.Paused || !Authority.IsAuthority || world.IsOver || IsComplete)
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
                    _running = false;
                    Completed?.Invoke(this);
                }
                return;
            }

            _waveTimer += deltaTime;
            if (_waveTimer >= WaveTimeout || (Wave > 0 && IsWaveMostlyCleared()))
                StartNextWave();
        }

        /// <summary>Starts the next wave immediately. For debug tools.</summary>
        public void SkipToNextWave()
        {
            if (_running && !IsComplete && (waveCount == 0 || Wave < waveCount))
                StartNextWave();
        }

        bool IsWaveMostlyCleared()
        {
            int allowed = Mathf.FloorToInt(_waveSize * (1f - config.ClearFraction));
            return RemainingInWave <= allowed;
        }

        void StartNextWave()
        {
            Wave++;
            _waveTimer = 0f;
            CurrentBudget = Mathf.Max(1, Mathf.RoundToInt(config.Budget(Wave, galaxyIndex, row) * BudgetMultiplier));
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

        /// <summary>A random arena point clear of every player. Also used by levels to place their own spawns.</summary>
        public Vector2 PickSpawnPoint()
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
