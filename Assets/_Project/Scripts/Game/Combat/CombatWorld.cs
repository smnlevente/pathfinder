using System;
using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
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

        /// <summary>Seed for the next world that starts. Consumed on use. Used by tests and seeded runs.</summary>
        public static Seed? NextSeedOverride { get; set; }

        public Seed Seed { get; private set; }
        public Vector2 ArenaSize => arenaSize;
        public ProjectileSystem Projectiles { get; private set; }

        /// <summary>Number of simulation ticks completed.</summary>
        public int Tick { get; private set; }

        /// <summary>Raised after each completed simulation tick.</summary>
        public event Action<CombatWorld> Ticked;

        /// <summary>Raised once, when the last living player goes down.</summary>
        public event Action AllPlayersDown;

        public bool IsOver { get; private set; }

        readonly List<CombatEntity> _scratch = new List<CombatEntity>(128);

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
            var projectileRoot = new GameObject("Projectiles").transform;
            projectileRoot.SetParent(transform, false);
            Projectiles = new ProjectileSystem(projectileRoot);
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
            Tick++;

            if (!IsOver && CombatRegistry.Players.Count > 0 && !CombatRegistry.AnyPlayerAlive())
            {
                IsOver = true;
                AllPlayersDown?.Invoke();
            }

            Ticked?.Invoke(this);
        }

        void LateUpdate() => Projectiles.UpdateVisuals(SimInterpolation.Alpha);

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
