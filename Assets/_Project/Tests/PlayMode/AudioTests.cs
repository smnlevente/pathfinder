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
        public IEnumerator GameplayRuns_WithoutADirector()
        {
            yield return LoadRun();
            Object.Destroy(Object.FindAnyObjectByType<GameAudio>().gameObject);
            yield return null;

            Assert.DoesNotThrow(() => GameAudio.Play(SoundCue.Roar), "Sound is optional: calls are safe without a director.");
        }
    }
}
