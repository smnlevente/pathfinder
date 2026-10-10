using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// Carries the run in progress across scene loads, from the Hub into the Run scene. A mid-run save will serialize
    /// this later (GDD §14.2).
    /// </summary>
    public static class RunSession
    {
        public static RunState Current { get; private set; }

        /// <summary>Starts a new run. With no seed, a fresh unpredictable one is generated (GDD §14.3).</summary>
        public static RunState Begin(Seed? seed = null)
        {
            Current = new RunState(seed ?? RunSeed.Generate());
            return Current;
        }

        public static void Clear() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;
    }
}
