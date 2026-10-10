using System;
using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// Owns the arena simulation and advances it in one place, in a fixed order each tick: players, then
    /// enemies, then projectiles. A single deterministic tick is what makes seeded runs reproducible and
    /// is the foundation for co-op netcode later (GDD §14.3, §14.4).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CombatWorld : MonoBehaviour
    {
        [SerializeField, Tooltip("Arena size in units, centred on the world origin.")]
        Vector2 arenaSize = new Vector2(60f, 40f);

        [SerializeField, Tooltip("Optional run seed code (e.g. 7-K3QP-9ZD2-XW4M). Leave empty for a random seed.")]
        string seedCode;

        [SerializeField, Tooltip("Visual for a Scraps pickup.")]
        GameObject scrapPickupPrefab;

        /// <summary>Seed for the next world that starts. Consumed on use. Used by tests and seeded runs.</summary>
        public static Seed? NextSeedOverride { get; set; }

        public Seed Seed { get; private set; }
        public Vector2 ArenaSize => arenaSize;
        public ProjectileSystem Projectiles { get; private set; }
        public PickupSystem Pickups { get; private set; }

        /// <summary>Number of simulation ticks completed.</summary>
        public int Tick { get; private set; }

        /// <summary>Raised after each completed simulation tick.</summary>
        public event Action<CombatWorld> Ticked;

        /// <summary>Raised once, when the last living player goes down.</summary>
        public event Action AllPlayersDown;

        public bool IsOver { get; private set; }

        /// <summary>Enemy health multiplier for the current level, e.g. ×1.6 in galaxy 2 (GDD §7.3).</summary>
        public float EnemyHealthMultiplier { get; private set; } = 1f;

        /// <summary>Scraps drop multiplier for the current level, e.g. ×2 in a Scraps level (GDD §10.1).</summary>
        public float ScrapsDropMultiplier { get; private set; } = 1f;

        readonly List<CombatEntity> _scratch = new List<CombatEntity>(128);
        int _enemySpawnCount;
        Seed _levelSeed;

        void Awake()
        {
            if (NextSeedOverride.HasValue)
            {
                Seed = NextSeedOverride.Value;
                NextSeedOverride = null;
            }
            else
            {
                Seed = !string.IsNullOrWhiteSpace(seedCode) && RunSeed.TryParse(seedCode, out var parsed) ? parsed : RunSeed.Generate();
            }

            CombatRegistry.ResetEntityIds();
            _levelSeed = Seed;
            var projectileRoot = new GameObject("Projectiles").transform;
            projectileRoot.SetParent(transform, false);
            Projectiles = new ProjectileSystem(projectileRoot);

            var pickupRoot = new GameObject("Pickups").transform;
            pickupRoot.SetParent(transform, false);
            Pickups = new PickupSystem(pickupRoot, scrapPickupPrefab);
        }

        void FixedUpdate()
        {
            _scratch.Clear();
            for (int team = 0; team < 2; team++)
                _scratch.AddRange(CombatRegistry.OnTeam((Team)team));

            // Even while paused, keep previous == current so interpolated visuals hold still.
            foreach (var entity in _scratch)
                entity.CapturePreviousPose();
            Projectiles.CapturePrevious();
            Pickups.CapturePrevious();

            if (GameClock.Paused)
                return;

            float deltaTime = Time.fixedDeltaTime;

            // _scratch holds players first, then enemies (team order), in registration order.
            foreach (var entity in _scratch)
            {
                if (entity.IsAlive)
                    entity.Simulate(deltaTime, this);
            }

            Projectiles.Simulate(deltaTime, this);
            Pickups.Simulate(deltaTime, this);
            Tick++;

            if (!IsOver && CombatRegistry.Players.Count > 0 && !CombatRegistry.AnyPlayerAlive())
            {
                IsOver = true;
                AllPlayersDown?.Invoke();
            }

            Ticked?.Invoke(this);
        }

        void LateUpdate()
        {
            float alpha = SimInterpolation.Alpha;
            Projectiles.UpdateVisuals(alpha);
            Pickups.UpdateVisuals(alpha);
        }

        /// <summary>
        /// Spawns an enemy. Authority only. Each spawn gets its own deterministic random streams, keyed by
        /// spawn order, so the same seed and inputs replay identically.
        /// </summary>
        public Enemy SpawnEnemy(EnemyDefinition definition, Vector2 position, EliteModifierDefinition elite = null, bool dropsLoot = true,
            float extraHealthMultiplier = 1f)
        {
            var seed = _levelSeed.Derive(SeedLabels.Enemy).Derive(_enemySpawnCount++);
            var enemy = Instantiate(definition.Prefab, PlaneMath.ToWorld(position), Quaternion.identity);
            enemy.Initialize(this, definition, seed.CreateRandom(SeedLabels.Ai), seed.CreateRandom(SeedLabels.Loot), elite, dropsLoot,
                EnemyHealthMultiplier * extraHealthMultiplier);
            return enemy;
        }

        /// <summary>
        /// Starts a new level in this arena. Enemy streams restart from the level seed, so a level plays the same
        /// regardless of which levels came before it (GDD §14.3).
        /// </summary>
        public void BeginLevel(Seed levelSeed, float enemyHealthMultiplier, float scrapsDropMultiplier)
        {
            _levelSeed = levelSeed;
            _enemySpawnCount = 0;
            EnemyHealthMultiplier = enemyHealthMultiplier;
            ScrapsDropMultiplier = scrapsDropMultiplier;
        }

        /// <summary>Removes everything a level left behind. Leftover Scraps go to the collector, if given.</summary>
        public void ClearLevel(PlayerShip collector)
        {
            var enemies = new List<Enemy>(CombatRegistry.Enemies);
            foreach (var enemy in enemies)
                enemy.Despawn();

            Projectiles.Clear();
            if (collector != null)
                Pickups.CollectAll(collector);
            Pickups.Clear();
        }

        public Vector2 ClampToArena(Vector2 position, float radius)
        {
            Vector2 half = arenaSize * 0.5f - new Vector2(radius, radius);
            return new Vector2(Mathf.Clamp(position.x, -half.x, half.x), Mathf.Clamp(position.y, -half.y, half.y));
        }

        public bool IsInsideArena(Vector2 position, float margin = 0f)
        {
            Vector2 half = arenaSize * 0.5f + new Vector2(margin, margin);
            return Mathf.Abs(position.x) <= half.x && Mathf.Abs(position.y) <= half.y;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, PlaneMath.ToWorld(arenaSize));
        }
    }
}
