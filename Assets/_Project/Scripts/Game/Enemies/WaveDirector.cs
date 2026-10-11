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
    /// itself in endless mode. Survival runs it endlessly too, with a budget set from its clock.
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
        float _firstWaveDelay;
        int _waveSize;
        bool _running;
        bool _keepToGalaxy;

        public WaveDirectorConfig Config => config;
        public CombatWorld World => world;
        public int Wave { get; private set; }
        public int WaveCount => waveCount;
        public int CurrentBudget { get; private set; }
        public bool IsRunning => _running;
        public bool IsComplete { get; private set; }

        /// <summary>Multiplies every wave budget, e.g. ×1.3 in Scraps levels.</summary>
        public float BudgetMultiplier { get; set; } = 1f;

        /// <summary>Seconds before the next wave starts anyway. Lower values keep pressure constant.</summary>
        public float WaveTimeout { get; set; }

        /// <summary>
        /// When above 0, every wave spends this budget (times <see cref="BudgetMultiplier"/>) instead of one that grows with
        /// each wave. Survival sets it from its clock, so pressure follows time rather than how fast waves are cleared.
        /// </summary>
        public int FixedBudget { get; set; }

        /// <summary>When 0 or more, each enemy's chance to be an elite, instead of one that grows with each wave. Survival sets it from its clock.</summary>
        public float FixedEliteChance { get; set; } = -1f;

        /// <summary>When above 0, no timed wave starts while this many enemies are alive. Keeps an endless arena from flooding.</summary>
        public int MaxEnemies { get; set; }

        /// <summary>Living enemies of the current wave, plus those still telegraphing.</summary>
        public int RemainingInWave => _waveEnemies.Count + _pending.Count;

        /// <summary>Raised when a wave's spawns are queued.</summary>
        public event Action<WaveDirector> WaveStarted;

        /// <summary>Raised once, when the final wave of a finite level is cleared.</summary>
        public event Action<WaveDirector> Completed;

        void Awake()
        {
            BuildEntries();
            WaveTimeout = config.WaveTimeout;
        }

        /// <summary>
        /// The pool for the current galaxy: enemies from later galaxies stay out (GDD §7.2), except that endless waves bring
        /// them in after a few waves, unless told to keep to the galaxy.
        /// </summary>
        void BuildEntries()
        {
            _entries.Clear();
            foreach (var entry in config.Enemies)
                _entries.Add(new WaveEntry(entry.Enemy.BudgetCost, entry.Weight, config.FirstWave(entry, galaxyIndex, endless: waveCount == 0 && !_keepToGalaxy)));
        }

        void Start()
        {
            if (autoStart && !_running)
                Begin(world.Seed.Derive(SeedLabels.Level).Derive(galaxyIndex).Derive(row), galaxyIndex, row, waveCount);
        }

        /// <summary>
        /// Starts spawning for a level. <paramref name="waves"/> = 0 is endless. The first wave arrives after
        /// <paramref name="firstWaveDelay"/> seconds (config default when negative). Endless waves keep to the galaxy's own
        /// enemies when <paramref name="keepToGalaxy"/> is set (Survival moves on with <see cref="SetGalaxy"/>).
        /// </summary>
        public void Begin(Seed levelSeed, int galaxy, int mapRow, int waves, float budgetMultiplier = 1f, float firstWaveDelay = -1f,
            bool keepToGalaxy = false)
        {
            Stop();
            _random = levelSeed.CreateRandom(SeedLabels.Waves);
            galaxyIndex = galaxy;
            row = mapRow;
            waveCount = waves;
            _keepToGalaxy = keepToGalaxy;
            BuildEntries();
            BudgetMultiplier = budgetMultiplier;
            FixedBudget = 0;
            FixedEliteChance = -1f;
            MaxEnemies = 0;
            WaveTimeout = config.WaveTimeout;
            Wave = 0;
            IsComplete = false;
            _waveSize = 0;
            // Timed on its own, so changing the wave timeout after Begin doesn't bring the first wave forward.
            _waveTimer = 0f;
            _firstWaveDelay = firstWaveDelay >= 0f ? firstWaveDelay : config.FirstWaveDelay;
            _running = true;
        }

        /// <summary>
        /// Moves the waves on to another galaxy's enemies and elite odds without restarting the wave count. Survival steps it
        /// up as each guardian falls.
        /// </summary>
        public void SetGalaxy(int galaxy)
        {
            galaxyIndex = Mathf.Max(0, galaxy);
            BuildEntries();
        }

        public int GalaxyIndex => galaxyIndex;

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
                // Done when nothing is left at all: drones a Carrier launched aren't wave enemies, but they still have to
                // die (playtest 7: a level used to finish with its last Carrier's drones still flying).
                if (_pending.Count == 0 && _waveEnemies.Count == 0 && !AnyEnemyAlive())
                {
                    IsComplete = true;
                    _running = false;
                    Completed?.Invoke(this);
                }
                return;
            }

            _waveTimer += deltaTime;
            bool crowded = MaxEnemies > 0 && CombatRegistry.Enemies.Count >= MaxEnemies;
            float due = Wave == 0 ? _firstWaveDelay : WaveTimeout;
            if ((_waveTimer >= due && !crowded) || (Wave > 0 && IsWaveMostlyCleared()))
                StartNextWave();
        }

        /// <summary>Starts the next wave immediately. For debug tools.</summary>
        public void SkipToNextWave()
        {
            if (_running && !IsComplete && (waveCount == 0 || Wave < waveCount))
                StartNextWave();
        }

        static bool AnyEnemyAlive()
        {
            foreach (var enemy in CombatRegistry.Enemies)
            {
                if (enemy != null && enemy.IsAlive && !enemy.Definition.IsHazard) // the galaxy's hazards don't count (M7)
                    return true;
            }
            return false;
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
            int budget = FixedBudget > 0 ? FixedBudget : config.Budget(Wave, galaxyIndex, row);
            // Bigger waves for each extra local co-op pilot (M8).
            float coop = Player.CoopRules.WaveBudgetScale(CombatRegistry.Players.Count);
            CurrentBudget = Mathf.Max(1, Mathf.RoundToInt(budget * BudgetMultiplier * coop));
            float eliteChance = config.Elites.Length == 0 ? 0f : FixedEliteChance >= 0f ? FixedEliteChance : config.EliteChance(Wave, galaxyIndex, row);

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
