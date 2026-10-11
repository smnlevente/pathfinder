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

        /// <summary>A continued run's pilots, player 1 first, each waiting to be put back once their ship spawns. See <see cref="RunSaveFile"/>.</summary>
        static RunSaveFile.Pilot[] s_pendingPilots = new RunSaveFile.Pilot[0];

        /// <summary>Whether a continued run still has pilots to put back.</summary>
        public static bool HasPendingPilots => System.Array.Exists(s_pendingPilots, pilot => pilot != null);

        /// <summary>Play time before this session, for a continued run's log.</summary>
        public static float MinutesBefore { get; private set; }

        /// <summary>Picks a saved run back up (M7).</summary>
        public static RunState Resume(RunSaveFile.Data data)
        {
            Current = RunSaveFile.ToRunState(data);
            s_pendingPilots = data.pilots != null && data.pilots.Count > 0 ? data.pilots.ToArray()
                : data.pilot != null ? new[] { data.pilot } : new RunSaveFile.Pilot[0];
            MinutesBefore = data.minutes;
            return Current;
        }

        /// <summary>Hands over a pending pilot once, to the spawner. Players beyond those saved start fresh.</summary>
        public static RunSaveFile.Pilot TakePendingPilot(int index = 0)
        {
            if (index < 0 || index >= s_pendingPilots.Length)
                return null;
            var pilot = s_pendingPilots[index];
            s_pendingPilots[index] = null;
            return pilot;
        }

        /// <summary>Starts a new run. With no seed, a fresh unpredictable one is generated (GDD §14.3).</summary>
        public static RunState Begin(Seed? seed = null, PilotLoadout loadout = default)
        {
            Current = new RunState(seed ?? RunSeed.Generate()) { Loadout = loadout };
            s_pendingPilots = new RunSaveFile.Pilot[0];
            MinutesBefore = 0f;
            return Current;
        }

        public static void Clear()
        {
            Current = null;
            s_pendingPilots = new RunSaveFile.Pilot[0];
            MinutesBefore = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }
}
