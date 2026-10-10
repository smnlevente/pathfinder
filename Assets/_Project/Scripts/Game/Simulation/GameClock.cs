using System.Collections.Generic;
using UnityEngine;

namespace Pathfinder.Game.Simulation
{
    /// <summary>
    /// Gameplay pause switch. The simulation never uses <c>Time.timeScale</c>, so pausing can't leak into
    /// UI or presentation, and co-op (where nothing pauses) can simply ignore this flag (GDD §14.4).
    /// Several systems can hold a pause at once (the pause menu, the galaxy map, a dialog). The game resumes
    /// only when every holder has released it.
    /// </summary>
    public static class GameClock
    {
        static readonly HashSet<object> s_holders = new HashSet<object>();

        public static bool Paused => s_holders.Count > 0;

        public static void Pause(object holder) => s_holders.Add(holder);

        public static void Resume(object holder) => s_holders.Remove(holder);

        public static bool IsPausedBy(object holder) => s_holders.Contains(holder);

        /// <summary>Drops every pause, e.g. when reloading a scene.</summary>
        public static void ResumeAll() => s_holders.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_holders.Clear();
    }
}
