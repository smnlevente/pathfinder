using System;
using System.Collections.Generic;
using Pathfinder.Game.Audio;
using UnityEditor;
using UnityEngine;
using static Pathfinder.Editor.Setup.Synth;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        const string AudioDir = Root + "/Art/Audio";

        /// <summary>
        /// Placeholder sound effects (GDD §13.2), synthesized into WAV files, and the sound library that maps cues to them.
        /// Clips are regenerated each build; a library entry's mix settings are created once and then left alone.
        /// </summary>
        static SoundLibrary BuildAudio()
        {
            EnsureFolder(AudioDir);
            var recipes = new (SoundCue cue, Func<float[]> make, float volume, float interval, int voices, float jitter)[]
            {
                (SoundCue.PlayerShot, PlayerShot, 0.32f, 0.05f, 3, 0.06f),
                (SoundCue.PlasmaShot, PlasmaShot, 0.5f, 0.08f, 2, 0.05f),
                (SoundCue.ChargeShot, ChargeShot, 0.6f, 0.1f, 2, 0.03f),
                (SoundCue.MissileLaunch, MissileLaunch, 0.5f, 0.1f, 2, 0.05f),
                (SoundCue.NovaPulse, NovaPulse, 0.75f, 0.2f, 1, 0f),
                (SoundCue.EnemyShot, EnemyShot, 0.22f, 0.07f, 3, 0.08f),
                (SoundCue.HitEnemy, HitEnemy, 0.25f, 0.04f, 3, 0.12f),
                (SoundCue.Deflect, Deflect, 0.3f, 0.06f, 3, 0.1f),
                (SoundCue.ShieldHit, ShieldHit, 0.55f, 0.08f, 2, 0.05f),
                (SoundCue.HullHit, HullHit, 0.75f, 0.1f, 2, 0.04f),
                (SoundCue.ExplosionSmall, ExplosionSmall, 0.5f, 0.05f, 4, 0.12f),
                (SoundCue.ExplosionLarge, ExplosionLarge, 0.8f, 0.1f, 2, 0.06f),
                (SoundCue.Dash, Dash, 0.45f, 0.1f, 2, 0.05f),
                (SoundCue.Pickup, Pickup, 0.3f, 0.06f, 3, 0.04f),
                (SoundCue.Telegraph, Telegraph, 0.45f, 0.15f, 3, 0.04f),
                (SoundCue.Roar, Roar, 0.85f, 0.5f, 1, 0.02f),
                (SoundCue.Erupt, Erupt, 0.85f, 0.2f, 2, 0.04f),
                (SoundCue.UiMove, UiMove, 0.3f, 0.03f, 2, 0.02f),
                (SoundCue.UiConfirm, UiConfirm, 0.45f, 0.05f, 2, 0f),
                (SoundCue.UiBack, UiBack, 0.4f, 0.05f, 2, 0f),
                (SoundCue.ExitOpen, ExitOpen, 0.55f, 0.5f, 1, 0f),
                (SoundCue.BossDefeated, BossDefeated, 0.75f, 1f, 1, 0f),
                (SoundCue.RunOver, RunOver, 0.7f, 1f, 1, 0f),
                (SoundCue.TelegraphLancer, TelegraphLancer, 0.4f, 0.2f, 2, 0.12f),
                (SoundCue.TelegraphGunner, TelegraphGunner, 0.3f, 0.2f, 2, 0.15f),
                (SoundCue.TelegraphSpinner, TelegraphSpinner, 0.32f, 0.25f, 2, 0.15f),
                (SoundCue.TelegraphCarrier, TelegraphCarrier, 0.45f, 0.4f, 1, 0.08f),
                (SoundCue.TelegraphScorcher, TelegraphScorcher, 0.35f, 0.25f, 2, 0.12f),
                (SoundCue.TelegraphArc, TelegraphArc, 0.35f, 0.2f, 2, 0.12f),
                (SoundCue.TelegraphCrusher, TelegraphCrusher, 0.5f, 0.3f, 2, 0.1f),
                (SoundCue.Flame, Flame, 0.4f, 0.3f, 3, 0.1f),
                (SoundCue.Zap, Zap, 0.55f, 0.08f, 3, 0.1f),
                (SoundCue.Slam, Slam, 0.8f, 0.15f, 2, 0.08f),
                (SoundCue.CoreOffline, CoreOffline, 0.8f, 0.3f, 1, 0f),
                (SoundCue.CoreReboot, CoreReboot, 0.7f, 0.3f, 1, 0f),
            };

            var clips = new Dictionary<SoundCue, AudioClip>();
            foreach (var recipe in recipes)
            {
                string path = $"{AudioDir}/SFX_{recipe.cue}.wav";
                var samples = recipe.make();
                Normalize(samples);
                WriteWav(path, samples);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is AudioImporter importer)
                {
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = true;
                    importer.SaveAndReimport();
                }
                clips[recipe.cue] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            var music = new Dictionary<string, AudioClip>();
            foreach (var (name, style) in new[] { ("Hub", HubMusic), ("Combat", CombatMusic), ("Boss", BossMusic) })
            {
                string path = $"{AudioDir}/Music_{name}.wav";
                WriteWav(path, Compose(style));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is AudioImporter importer)
                {
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.7f;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = true;
                    importer.SaveAndReimport();
                }
                music[name] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            const string libraryPath = DataDir + "/Audio/SoundLibrary.asset";
            EnsureFolder(DataDir + "/Audio");
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }

            // Keep existing entries' mix settings; add missing cues with defaults; always point at the fresh clips.
            var so = new SerializedObject(library);
            var entries = so.FindProperty("<Entries>k__BackingField");
            foreach (var recipe in recipes)
            {
                SerializedProperty entry = null;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Cue").intValue == (int)recipe.cue)
                        entry = entries.GetArrayElementAtIndex(i);
                }
                if (entry == null)
                {
                    entries.arraySize++;
                    entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    entry.FindPropertyRelative("Cue").intValue = (int)recipe.cue;
                    entry.FindPropertyRelative("Volume").floatValue = recipe.volume;
                    entry.FindPropertyRelative("MinInterval").floatValue = recipe.interval;
                    entry.FindPropertyRelative("MaxVoices").intValue = recipe.voices;
                    entry.FindPropertyRelative("PitchJitter").floatValue = recipe.jitter;
                }
                entry.FindPropertyRelative("Clip").objectReferenceValue = clips[recipe.cue];
            }
            so.FindProperty("<HubMusic>k__BackingField").objectReferenceValue = music["Hub"];
            so.FindProperty("<CombatMusic>k__BackingField").objectReferenceValue = music["Combat"];
            so.FindProperty("<BossMusic>k__BackingField").objectReferenceValue = music["Boss"];
            so.ApplyModifiedPropertiesWithoutUndo();
            return library;
        }

        // ---------------------------------------------------------------- recipes

        static float[] PlayerShot()
        {
            var b = Samples(0.09f);
            Tone(b, 0f, 0.09f, Wave.Square, 1500f, 520f, 0.5f, decayPower: 2.5f);
            Tone(b, 0f, 0.03f, Wave.Noise, 9000f, 4000f, 0.25f, seed: 2);
            LowPass(b, 6000f, 1800f);
            return b;
        }

        static float[] PlasmaShot()
        {
            var b = Samples(0.18f);
            Tone(b, 0f, 0.18f, Wave.Noise, 7000f, 900f, 0.8f, decayPower: 2f, seed: 3);
            Tone(b, 0f, 0.12f, Wave.Sine, 160f, 60f, 0.7f, decayPower: 1.5f);
            LowPass(b, 5000f, 700f);
            return b;
        }

        static float[] ChargeShot()
        {
            var b = Samples(0.35f);
            Tone(b, 0f, 0.35f, Wave.Saw, 320f, 90f, 0.5f, decayPower: 1.6f);
            Tone(b, 0f, 0.25f, Wave.Sine, 1200f, 300f, 0.4f, decayPower: 2f);
            Tone(b, 0f, 0.1f, Wave.Noise, 8000f, 2000f, 0.4f, seed: 4);
            LowPass(b, 7000f, 900f);
            return b;
        }

        static float[] MissileLaunch()
        {
            var b = Samples(0.3f);
            Tone(b, 0f, 0.3f, Wave.Noise, 2000f, 6000f, 0.6f, attack: 0.03f, decayPower: 1.5f, seed: 5);
            Tone(b, 0f, 0.25f, Wave.Saw, 180f, 420f, 0.25f, attack: 0.02f);
            LowPass(b, 2500f, 5000f);
            return b;
        }

        static float[] NovaPulse()
        {
            var b = Samples(0.9f);
            Tone(b, 0f, 0.9f, Wave.Sine, 140f, 40f, 0.9f, attack: 0.01f, decayPower: 1.4f);
            Tone(b, 0f, 0.7f, Wave.Noise, 4000f, 300f, 0.5f, attack: 0.02f, decayPower: 2f, seed: 6);
            Tone(b, 0f, 0.5f, Wave.Sine, 700f, 1400f, 0.2f, decayPower: 2f);
            LowPass(b, 5000f, 400f);
            return b;
        }

        static float[] EnemyShot()
        {
            var b = Samples(0.12f);
            Tone(b, 0f, 0.12f, Wave.Triangle, 620f, 300f, 0.7f, decayPower: 2f);
            LowPass(b, 3000f, 1200f);
            return b;
        }

        static float[] HitEnemy()
        {
            var b = Samples(0.06f);
            Tone(b, 0f, 0.06f, Wave.Noise, 12000f, 5000f, 0.7f, decayPower: 3f, seed: 7);
            Tone(b, 0f, 0.04f, Wave.Square, 900f, 500f, 0.25f, decayPower: 3f);
            return b;
        }

        static float[] Deflect()
        {
            var b = Samples(0.14f);
            Tone(b, 0f, 0.14f, Wave.Sine, 2600f, 2500f, 0.6f, decayPower: 3f);
            Tone(b, 0f, 0.1f, Wave.Sine, 3900f, 3800f, 0.35f, decayPower: 3.5f);
            Tone(b, 0f, 0.015f, Wave.Noise, 10000f, 8000f, 0.3f, seed: 8);
            return b;
        }

        static float[] ShieldHit()
        {
            var b = Samples(0.22f);
            Tone(b, 0f, 0.22f, Wave.Sine, 1500f, 700f, 0.6f, decayPower: 2f, vibratoHz: 30f, vibratoDepth: 0.03f);
            Tone(b, 0f, 0.08f, Wave.Noise, 6000f, 3000f, 0.25f, seed: 9);
            return b;
        }

        static float[] HullHit()
        {
            var b = Samples(0.3f);
            Tone(b, 0f, 0.3f, Wave.Square, 230f, 90f, 0.6f, decayPower: 1.8f);
            Tone(b, 0f, 0.2f, Wave.Noise, 5000f, 600f, 0.7f, decayPower: 2f, seed: 10);
            LowPass(b, 4000f, 600f);
            return b;
        }

        static float[] ExplosionSmall()
        {
            var b = Samples(0.45f);
            Tone(b, 0f, 0.45f, Wave.Noise, 5000f, 200f, 1f, decayPower: 2.2f, seed: 11);
            Tone(b, 0f, 0.3f, Wave.Sine, 120f, 40f, 0.7f, decayPower: 1.6f);
            LowPass(b, 4000f, 250f);
            return b;
        }

        static float[] ExplosionLarge()
        {
            var b = Samples(1.1f);
            Tone(b, 0f, 1.1f, Wave.Noise, 3500f, 80f, 1f, attack: 0.005f, decayPower: 1.8f, seed: 12);
            Tone(b, 0f, 0.8f, Wave.Sine, 90f, 30f, 0.9f, decayPower: 1.4f);
            Tone(b, 0.08f, 0.6f, Wave.Noise, 2000f, 100f, 0.5f, decayPower: 2f, seed: 13);
            LowPass(b, 3000f, 150f);
            return b;
        }

        static float[] Dash()
        {
            var b = Samples(0.28f);
            Tone(b, 0f, 0.28f, Wave.Noise, 800f, 7000f, 0.8f, attack: 0.04f, decayPower: 1.6f, seed: 14);
            LowPass(b, 1500f, 6000f);
            return b;
        }

        static float[] Pickup()
        {
            var b = Samples(0.14f);
            Tone(b, 0f, 0.07f, Wave.Sine, 1320f, 1320f, 0.6f, decayPower: 1.2f);
            Tone(b, 0.05f, 0.09f, Wave.Sine, 1980f, 1980f, 0.6f, decayPower: 1.5f);
            return b;
        }

        static float[] Telegraph()
        {
            var b = Samples(0.45f);
            Tone(b, 0f, 0.45f, Wave.Saw, 260f, 620f, 0.45f, attack: 0.05f, decayPower: 0.7f);
            Tone(b, 0f, 0.45f, Wave.Sine, 520f, 1240f, 0.3f, attack: 0.05f, decayPower: 0.7f);
            LowPass(b, 1500f, 4000f);
            return b;
        }

        /// <summary>Lancer: a rising charge-up whine, the length of its wind-up.</summary>
        static float[] TelegraphLancer()
        {
            var b = Samples(0.6f);
            Tone(b, 0f, 0.6f, Wave.Saw, 180f, 950f, 0.4f, attack: 0.08f, decayPower: 0.5f, vibratoHz: 14f, vibratoDepth: 0.02f);
            Tone(b, 0f, 0.6f, Wave.Noise, 1500f, 7000f, 0.15f, attack: 0.2f, decayPower: 0.6f, seed: 30);
            LowPass(b, 1200f, 5000f);
            return b;
        }

        /// <summary>Gunner: a short two-tone lock-on beep.</summary>
        static float[] TelegraphGunner()
        {
            var b = Samples(0.22f);
            Tone(b, 0f, 0.08f, Wave.Square, 880f, 880f, 0.35f, decayPower: 1f);
            Tone(b, 0.11f, 0.09f, Wave.Square, 1175f, 1175f, 0.35f, decayPower: 1f);
            LowPass(b, 3500f, 3500f);
            return b;
        }

        /// <summary>Spinner: a quick warble as it spins up.</summary>
        static float[] TelegraphSpinner()
        {
            var b = Samples(0.4f);
            Tone(b, 0f, 0.4f, Wave.Triangle, 420f, 780f, 0.5f, attack: 0.03f, decayPower: 0.8f, vibratoHz: 22f, vibratoDepth: 0.12f);
            return b;
        }

        /// <summary>Carrier: a low horn before it launches its drones.</summary>
        static float[] TelegraphCarrier()
        {
            var b = Samples(0.7f);
            Tone(b, 0f, 0.7f, Wave.Saw, 98f, 131f, 0.6f, attack: 0.1f, decayPower: 0.9f);
            Tone(b, 0f, 0.7f, Wave.Saw, 147f, 196f, 0.3f, attack: 0.12f, decayPower: 1f);
            LowPass(b, 900f, 1600f);
            return b;
        }

        /// <summary>Scorcher: a pilot light catching, hiss rising over a low rumble.</summary>
        static float[] TelegraphScorcher()
        {
            var b = Samples(0.6f);
            Tone(b, 0f, 0.6f, Wave.Noise, 900f, 3500f, 0.5f, attack: 0.25f, decayPower: 0.4f, seed: 40);
            Tone(b, 0f, 0.6f, Wave.Saw, 70f, 110f, 0.35f, attack: 0.1f, decayPower: 0.7f);
            LowPass(b, 1500f, 4000f);
            return b;
        }

        /// <summary>Arc Drone: an electric charge-up buzz that climbs and crackles.</summary>
        static float[] TelegraphArc()
        {
            var b = Samples(0.7f);
            Tone(b, 0f, 0.7f, Wave.Square, 110f, 620f, 0.35f, attack: 0.05f, decayPower: 0.3f, vibratoHz: 40f, vibratoDepth: 0.15f);
            Tone(b, 0.1f, 0.6f, Wave.Noise, 6000f, 12000f, 0.15f, attack: 0.3f, decayPower: 0.5f, seed: 41);
            LowPass(b, 2500f, 6000f);
            return b;
        }

        /// <summary>Crusher: a heavy servo groan as it rears up.</summary>
        static float[] TelegraphCrusher()
        {
            var b = Samples(0.55f);
            Tone(b, 0f, 0.55f, Wave.Saw, 62f, 45f, 0.6f, attack: 0.06f, decayPower: 0.6f);
            Tone(b, 0f, 0.55f, Wave.Triangle, 190f, 120f, 0.3f, attack: 0.08f, decayPower: 0.8f, vibratoHz: 9f, vibratoDepth: 0.05f);
            LowPass(b, 900f, 500f);
            return b;
        }

        /// <summary>Scorcher spray: a roaring whoosh as long as the spray.</summary>
        static float[] Flame()
        {
            var b = Samples(1.4f);
            Tone(b, 0f, 1.4f, Wave.Noise, 1400f, 900f, 0.6f, attack: 0.06f, decayPower: 0.5f, seed: 42);
            Tone(b, 0f, 1.4f, Wave.Noise, 300f, 200f, 0.4f, attack: 0.05f, decayPower: 0.6f, seed: 43);
            LowPass(b, 2200f, 1200f);
            return b;
        }

        /// <summary>Arc Drone discharge: a sharp electric crack.</summary>
        static float[] Zap()
        {
            var b = Samples(0.2f);
            Tone(b, 0f, 0.12f, Wave.Noise, 12000f, 2500f, 0.6f, decayPower: 2f, seed: 44);
            Tone(b, 0f, 0.18f, Wave.Square, 1800f, 160f, 0.35f, decayPower: 1.8f);
            LowPass(b, 9000f, 2500f);
            return b;
        }

        /// <summary>Crusher slam: a deep thud and a scatter of debris.</summary>
        static float[] Slam()
        {
            var b = Samples(0.6f);
            Tone(b, 0f, 0.6f, Wave.Sine, 95f, 32f, 0.9f, decayPower: 1.6f);
            Tone(b, 0f, 0.35f, Wave.Noise, 2500f, 200f, 0.5f, decayPower: 1.8f, seed: 45);
            LowPass(b, 3000f, 300f);
            return b;
        }

        /// <summary>A Warden core powering down: a falling whine.</summary>
        static float[] CoreOffline()
        {
            var b = Samples(0.9f);
            Tone(b, 0f, 0.9f, Wave.Saw, 620f, 55f, 0.5f, decayPower: 0.8f);
            Tone(b, 0f, 0.25f, Wave.Noise, 5000f, 800f, 0.35f, decayPower: 1.5f, seed: 46);
            LowPass(b, 4000f, 600f);
            return b;
        }

        /// <summary>A Warden core rebooting: a rising sweep and a chime.</summary>
        static float[] CoreReboot()
        {
            var b = Samples(0.75f);
            Tone(b, 0f, 0.55f, Wave.Triangle, 150f, 900f, 0.5f, attack: 0.05f, decayPower: 0.5f);
            Tone(b, 0.5f, 0.25f, Wave.Sine, 1320f, 1320f, 0.4f, decayPower: 1.2f);
            Tone(b, 0.5f, 0.25f, Wave.Sine, 1980f, 1980f, 0.2f, decayPower: 1.5f);
            return b;
        }

        static float[] Roar()
        {
            var b = Samples(1.5f);
            Tone(b, 0f, 1.5f, Wave.Saw, 85f, 55f, 0.8f, attack: 0.15f, decayPower: 1.2f, vibratoHz: 9f, vibratoDepth: 0.08f);
            Tone(b, 0f, 1.4f, Wave.Saw, 128f, 80f, 0.4f, attack: 0.2f, decayPower: 1.4f, vibratoHz: 7f, vibratoDepth: 0.1f);
            Tone(b, 0f, 1.3f, Wave.Noise, 1200f, 300f, 0.5f, attack: 0.1f, decayPower: 1.6f, seed: 15);
            LowPass(b, 1800f, 500f);
            return b;
        }

        static float[] Erupt()
        {
            var b = Samples(0.9f);
            Tone(b, 0f, 0.9f, Wave.Noise, 2500f, 90f, 1f, decayPower: 1.7f, seed: 16);
            Tone(b, 0f, 0.6f, Wave.Sine, 70f, 32f, 1f, decayPower: 1.3f);
            Tone(b, 0f, 0.05f, Wave.Noise, 9000f, 6000f, 0.6f, seed: 17);
            LowPass(b, 3500f, 180f);
            return b;
        }

        static float[] UiMove()
        {
            var b = Samples(0.04f);
            Tone(b, 0f, 0.04f, Wave.Sine, 2100f, 1900f, 0.6f, decayPower: 3f);
            return b;
        }

        static float[] UiConfirm()
        {
            var b = Samples(0.16f);
            Tone(b, 0f, 0.07f, Wave.Triangle, 660f, 660f, 0.6f, decayPower: 1.2f);
            Tone(b, 0.06f, 0.1f, Wave.Triangle, 990f, 990f, 0.6f, decayPower: 1.5f);
            return b;
        }

        static float[] UiBack()
        {
            var b = Samples(0.16f);
            Tone(b, 0f, 0.07f, Wave.Triangle, 760f, 760f, 0.55f, decayPower: 1.2f);
            Tone(b, 0.06f, 0.1f, Wave.Triangle, 500f, 500f, 0.55f, decayPower: 1.5f);
            return b;
        }

        static float[] ExitOpen()
        {
            var b = Samples(0.8f);
            float[] notes = { 523f, 659f, 784f, 1047f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.09f, 0.5f, Wave.Triangle, notes[i], notes[i], 0.4f, decayPower: 1.6f);
            Tone(b, 0f, 0.8f, Wave.Noise, 3000f, 9000f, 0.08f, attack: 0.2f, decayPower: 1f, seed: 18);
            return b;
        }

        static float[] BossDefeated()
        {
            var b = Samples(1.8f);
            float[] notes = { 392f, 523f, 659f, 784f, 1047f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.12f, 1.2f, Wave.Saw, notes[i], notes[i], 0.22f, attack: 0.01f, decayPower: 1.8f);
            Tone(b, 0.6f, 1.2f, Wave.Triangle, 1047f, 1047f, 0.35f, decayPower: 1.2f);
            Tone(b, 0.6f, 1.2f, Wave.Triangle, 1319f, 1319f, 0.25f, decayPower: 1.2f);
            LowPass(b, 6000f, 2500f);
            return b;
        }

        static float[] RunOver()
        {
            var b = Samples(1.6f);
            float[] notes = { 440f, 349f, 294f, 220f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.25f, 0.8f, Wave.Triangle, notes[i], notes[i] * 0.99f, 0.4f, attack: 0.02f, decayPower: 1.5f);
            LowPass(b, 3000f, 900f);
            return b;
        }
    }
}
