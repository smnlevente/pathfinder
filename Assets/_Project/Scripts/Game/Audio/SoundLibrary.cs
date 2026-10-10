using System;
using UnityEngine;

namespace Pathfinder.Game.Audio
{
    /// <summary>Every sound the game plays (GDD §13.2). Gameplay asks for a cue; the library decides what it sounds like.</summary>
    public enum SoundCue
    {
        PlayerShot,
        PlasmaShot,
        ChargeShot,
        MissileLaunch,
        NovaPulse,
        EnemyShot,
        HitEnemy,
        Deflect,
        ShieldHit,
        HullHit,
        ExplosionSmall,
        ExplosionLarge,
        Dash,
        Pickup,
        Telegraph,
        Roar,
        Erupt,
        UiMove,
        UiConfirm,
        UiBack,
        ExitOpen,
        BossDefeated,
        RunOver,

        // Each enemy type winds up with its own sound, so a crowd doesn't drone (playtest 4).
        TelegraphLancer,
        TelegraphGunner,
        TelegraphSpinner,
        TelegraphCarrier,

        // Galaxy 2 enemies and the Binary Wardens.
        TelegraphScorcher,
        TelegraphArc,
        TelegraphCrusher,
        Flame,
        Zap,
        Slam,
        CoreOffline,
        CoreReboot,

        // Playtest 5.
        SecondaryReady,
        WarpIn,
    }

    /// <summary>
    /// Background music, by context (GDD §13.2). <see cref="Combat"/> is the first galaxy's track; each galaxy has its own
    /// (playtest 5). Append new tracks at the end: scenes store these as numbers.
    /// </summary>
    public enum MusicTrack { None, Hub, Combat, Boss, Galaxy2, Galaxy3 }

    /// <summary>The clips and mix settings for each <see cref="SoundCue"/>, and the music tracks.</summary>
    [CreateAssetMenu(menuName = "Pathfinder/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SoundCue Cue;
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume = 0.7f;
            [Range(0f, 0.5f), Tooltip("Random pitch variation, so repeated sounds don't machine-gun.")] public float PitchJitter = 0.05f;
            [Min(0f), Tooltip("Minimum seconds between two plays of this cue.")] public float MinInterval = 0.03f;
            [Min(1), Tooltip("Most copies of this cue playing at once.")] public int MaxVoices = 4;
        }

        [field: SerializeField] public Entry[] Entries { get; private set; } = Array.Empty<Entry>();
        [field: SerializeField] public AudioClip HubMusic { get; private set; }
        [field: SerializeField] public AudioClip CombatMusic { get; private set; }
        [field: SerializeField] public AudioClip BossMusic { get; private set; }
        [field: SerializeField] public AudioClip Galaxy2Music { get; private set; }
        [field: SerializeField] public AudioClip Galaxy3Music { get; private set; }

        public AudioClip Music(MusicTrack track) => track switch
        {
            MusicTrack.Hub => HubMusic,
            MusicTrack.Combat => CombatMusic,
            MusicTrack.Boss => BossMusic,
            MusicTrack.Galaxy2 => Galaxy2Music,
            MusicTrack.Galaxy3 => Galaxy3Music,
            _ => null,
        };

        /// <summary>The track for a galaxy's map and levels (0-based). Bosses have their own.</summary>
        public static MusicTrack ForGalaxy(int galaxyIndex) => galaxyIndex switch
        {
            <= 0 => MusicTrack.Combat,
            1 => MusicTrack.Galaxy2,
            _ => MusicTrack.Galaxy3,
        };

        public Entry Find(SoundCue cue)
        {
            foreach (var entry in Entries)
            {
                if (entry != null && entry.Cue == cue)
                    return entry;
            }
            return null;
        }
    }
}
