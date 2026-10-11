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

        /// <summary>A continued run's pilot, waiting to be put back once the ship spawns. See <see cref="RunSaveFile"/>.</summary>
        public static RunSaveFile.Pilot PendingPilot { get; private set; }

        /// <summary>Play time before this session, for a continued run's log.</summary>
        public static float MinutesBefore { get; private set; }

        /// <summary>Picks a saved run back up (M7).</summary>
        public static RunState Resume(RunSaveFile.Data data)
        {
            Current = RunSaveFile.ToRunState(data);
            PendingPilot = data.pilot;
            MinutesBefore = data.minutes;
            return Current;
        }

        /// <summary>Hands over the pending pilot once, to the spawner.</summary>
        public static RunSaveFile.Pilot TakePendingPilot()
        {
            var pilot = PendingPilot;
            PendingPilot = null;
            return pilot;
        }

        /// <summary>Starts a new run. With no seed, a fresh unpredictable one is generated (GDD §14.3).</summary>
        public static RunState Begin(Seed? seed = null, PilotLoadout loadout = default)
        {
            Current = new RunState(seed ?? RunSeed.Generate()) { Loadout = loadout };
            PendingPilot = null;
            MinutesBefore = 0f;
            return Current;
        }

        public static void Clear()
        {
            Current = null;
            PendingPilot = null;
            MinutesBefore = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }
}
