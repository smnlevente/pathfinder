using System;
using System.IO;
using Pathfinder.Core.Random;
using Pathfinder.Game.Meta;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// A one-line summary of every finished run, appended to run-log.jsonl next to the meta save (GDD §17, M6). Playtesters
    /// send the file back; each line has the seed, so any run can be replayed. Never blocks the game.
    /// </summary>
    public static class RunLog
    {
        [Serializable]
        public struct Entry
        {
            public string timeUtc;
            public string seed;
            public string ship, primary, secondary;
            public bool won;
            public int galaxyReached, levelsCleared;
            public int darkMatter, singularityCores, scrapsCollected;
            public float minutes;
            public string killedBy;
        }

        /// <summary>The log file, in the same folder as the meta save (so tests that redirect the save redirect this too).</summary>
        public static string FilePath => Path.Combine(Path.GetDirectoryName(MetaProgression.SavePath) ?? Application.persistentDataPath, "run-log.jsonl");

        public static void Append(Entry entry)
        {
            try
            {
                File.AppendAllText(FilePath, JsonUtility.ToJson(entry) + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunLog] Could not write {FilePath}: {e.Message}");
            }
        }

        public static string SeedCode(Seed seed) => RunSeed.ToCode(seed);
    }
}
