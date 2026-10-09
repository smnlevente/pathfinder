using UnityEngine;

namespace Pathfinder.Game.Simulation
{
    /// <summary>
    /// Gameplay pause switch. The simulation never uses <c>Time.timeScale</c>, so pausing can't leak into
    /// UI or presentation, and co-op (where nothing pauses) can simply ignore this flag (GDD §14.4).
    /// </summary>
    public static class GameClock
    {
        public static bool Paused { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Paused = false;
    }
}
