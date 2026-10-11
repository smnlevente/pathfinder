using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Local co-op's rules (M8, GDD §14.5): up to four pilots on one screen. A downed pilot stays as a wreck that a
    /// teammate brings back by hovering next to it; enemies are tougher and waves bigger for each extra pilot.
    /// </summary>
    public static class CoopRules
    {
        public const int MaxPilots = 4;

        /// <summary>Seconds a teammate hovers next to a wreck to bring it back.</summary>
        public const float ReviveTime = 2f;

        /// <summary>How close (centre to centre) a teammate must be to the wreck.</summary>
        public const float ReviveRadius = 2.5f;

        /// <summary>A revived pilot comes back with this much of their hull.</summary>
        public const float ReviveHullFraction = 0.15f;

        /// <summary>Seconds a revived pilot can't be hit, to get clear.</summary>
        public const float ReviveGrace = 2f;

        /// <summary>Without a teammate next to it, a wreck's revive progress drains at this fraction of the rate it fills.</summary>
        public const float ReviveDrain = 0.5f;

        public const float EnemyHealthPerExtraPilot = 0.65f;
        public const float WaveBudgetPerExtraPilot = 0.5f;

        /// <summary>Enemies' health multiplier for this many pilots: ×1.65 for two, ×2.3 for three.</summary>
        public static float EnemyHealthScale(int pilots) => 1f + EnemyHealthPerExtraPilot * Mathf.Max(0, pilots - 1);

        /// <summary>Wave budget multiplier for this many pilots: ×1.5 for two, ×2 for three.</summary>
        public static float WaveBudgetScale(int pilots) => 1f + WaveBudgetPerExtraPilot * Mathf.Max(0, pilots - 1);

        static readonly Color[] Colors =
        {
            new Color(0.35f, 0.9f, 1f),  // cyan
            new Color(1f, 0.8f, 0.3f),   // gold
            new Color(0.78f, 0.55f, 1f), // violet
            new Color(1f, 0.5f, 0.72f),  // pink
        };

        /// <summary>Each pilot's own colour: their ring, tag and panel.</summary>
        public static Color PilotColor(int index) => Colors[Mathf.Clamp(index, 0, Colors.Length - 1)];

        /// <summary>"P1", "P2"…</summary>
        public static string Tag(int index) => $"P{index + 1}";

        /// <summary>Where a pilot arrives relative to the level's entry point: side by side, 2.2 u apart.</summary>
        public static Vector2 EntryOffset(int index, int count) =>
            count <= 1 ? Vector2.zero : new Vector2((index - (count - 1) * 0.5f) * 2.2f, 0f);
    }
}
