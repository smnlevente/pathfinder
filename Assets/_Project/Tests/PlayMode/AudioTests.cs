using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Sound (GDD §13.2): every cue has a clip, and gameplay events reach the sound director.</summary>
    public class AudioTests : SandboxTestBase
    {
        static SoundLibrary Library() => Resources.FindObjectsOfTypeAll<SoundLibrary>().First();

        /// <summary>Batch mode may run without an audio device, so check which clip each voice was given, not isPlaying.</summary>
        static bool AnyVoiceHolds(AudioClip clip) =>
            Object.FindAnyObjectByType<GameAudio>().GetComponents<AudioSource>().Any(source => source.clip == clip);

        [UnityTest]
        public IEnumerator EveryCue_HasASound()
        {
            yield return LoadRun();
            Assert.IsNotNull(Object.FindAnyObjectByType<GameAudio>(), "Every scene has a sound director.");

            var library = Library();
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                var entry = library.Find(cue);
                Assert.IsNotNull(entry, $"{cue} has a library entry.");
                Assert.IsNotNull(entry.Clip, $"{cue} has a clip.");
                Assert.Greater(entry.Clip.length, 0.01f, $"{cue} isn't silent air.");
            }
        }

        [UnityTest]
        public IEnumerator CombatEvents_PlaySounds()
        {
            yield return LoadRun();
            var library = Library();

            var drone = Spawn("enemy.drone", new Vector2(0f, 5f));
            drone.ApplyDamage(new DamageInfo(10_000f, Team.Player, Player.EntityId, drone.Position, Vector2.up));
            yield return null;
            Assert.IsTrue(AnyVoiceHolds(library.Find(SoundCue.ExplosionSmall).Clip), "A kill explodes audibly.");

            Player.ApplyDamage(new DamageInfo(5f, Team.Enemy, 0, Player.Position, Vector2.up));
            yield return null;
            Assert.IsTrue(AnyVoiceHolds(library.Find(SoundCue.ShieldHit).Clip), "Shield hits have their own sound.");
        }

        [UnityTest]
        public IEnumerator Music_HasLoopsForEachContext()
        {
            yield return LoadRun();
            yield return null;
            var library = Library();
            foreach (var track in new[] { MusicTrack.Hub, MusicTrack.Combat, MusicTrack.Boss })
            {
                Assert.IsNotNull(library.Music(track), $"{track} music exists.");
                Assert.Greater(library.Music(track).length, 10f, $"{track} music is a real loop, not a jingle.");
            }
            Assert.AreEqual(MusicTrack.Combat, GameAudio.CurrentMusic, "The combat sandbox plays the combat track.");
        }

        [UnityTest]
        public IEnumerator Music_IsAudible()
        {
            yield return LoadRun();
            if (AudioSettings.outputSampleRate <= 0)
                Assert.Ignore("No audio device in this environment.");

            float until = Time.realtimeSinceStartup + 1.5f; // past the fade-in
            while (Time.realtimeSinceStartup < until)
                yield return null;

            var music = Library().Music(MusicTrack.Combat);
            var source = Object.FindAnyObjectByType<GameAudio>().GetComponents<AudioSource>().FirstOrDefault(s => s.clip == music);
            Assert.IsNotNull(source, "A music voice holds the combat track.");
            Assert.IsTrue(source.isPlaying, "The music is playing.");
            Assert.Greater(source.volume, 0.2f, "Loud enough to hear at default settings.");
        }

        [UnityTest]
        public IEnumerator GameplayRuns_WithoutADirector()
        {
            yield return LoadRun();
            Object.Destroy(Object.FindAnyObjectByType<GameAudio>().gameObject);
            yield return null;

            Assert.DoesNotThrow(() => GameAudio.Play(SoundCue.Roar), "Sound is optional: calls are safe without a director.");
        }
    }
}
