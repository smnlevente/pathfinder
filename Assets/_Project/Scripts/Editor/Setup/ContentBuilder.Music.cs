using static Pathfinder.Editor.Setup.Synth;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        /// <summary>One looping placeholder track (GDD §13.2): tempo, chord progression and which layers play.</summary>
        sealed class MusicStyle
        {
            public float Bpm;
            public int Bars = 8;
            public int BarsPerChord = 2;

            /// <summary>Each chord: the bass root, then the pad voicing, as MIDI note numbers.</summary>
            public int[][] Chords;

            public int BassStepsPerBar = 8;
            public bool Drums, FourOnTheFloor, Arpeggio;
            public float PadCutoff = 1500f, PadAttack = 0.25f;
        }

        // A minor throughout. Hub: calm pads. Combat: driving synthwave. Boss: faster, darker, four on the floor.
        static readonly MusicStyle HubMusic = new MusicStyle
        {
            Bpm = 80f, BassStepsPerBar = 2, Arpeggio = true, PadCutoff = 1100f, PadAttack = 0.9f,
            Chords = new[] { new[] { 45, 57, 60, 64 }, new[] { 40, 55, 59, 64 }, new[] { 41, 57, 60, 65 }, new[] { 36, 55, 60, 64 } },
        };

        static readonly MusicStyle CombatMusic = new MusicStyle
        {
            Bpm = 110f, Drums = true, Arpeggio = true,
            Chords = new[] { new[] { 45, 57, 60, 64 }, new[] { 41, 57, 60, 65 }, new[] { 36, 55, 60, 64 }, new[] { 43, 55, 59, 62 } },
        };

        static readonly MusicStyle BossMusic = new MusicStyle
        {
            Bpm = 128f, BassStepsPerBar = 16, Drums = true, FourOnTheFloor = true, Arpeggio = true, PadCutoff = 2000f, PadAttack = 0.1f,
            Chords = new[] { new[] { 45, 57, 60, 64 }, new[] { 46, 58, 62, 65 }, new[] { 45, 57, 60, 64 }, new[] { 40, 56, 59, 64 } },
        };

        /// <summary>Renders a style into a seamless loop: notes ringing past the end wrap to the start.</summary>
        static float[] Compose(MusicStyle style)
        {
            float beat = 60f / style.Bpm, bar = beat * 4f;
            var bass = Samples(bar * style.Bars);
            var pad = Samples(bar * style.Bars);
            var lead = Samples(bar * style.Bars);
            var drums = Samples(bar * style.Bars);

            for (int b = 0; b < style.Bars; b++)
            {
                var chord = style.Chords[b / style.BarsPerChord % style.Chords.Length];
                float barStart = b * bar;

                // Pads: two slightly detuned saws per chord tone, held for the whole chord.
                if (b % style.BarsPerChord == 0)
                {
                    for (int n = 1; n < chord.Length; n++)
                    {
                        float length = bar * style.BarsPerChord;
                        Note(pad, barStart, length, Wave.Saw, Midi(chord[n]), 0.1f, style.PadAttack, 0.5f, detune: 0.004f, seed: n);
                        Note(pad, barStart, length, Wave.Saw, Midi(chord[n]), 0.1f, style.PadAttack, 0.5f, detune: -0.004f, seed: n + 10);
                    }
                }

                // Bass: the root, jumping an octave on every fourth step.
                float step = bar / style.BassStepsPerBar;
                for (int s = 0; s < style.BassStepsPerBar; s++)
                {
                    int note = chord[0] + (style.BassStepsPerBar >= 8 && s % 4 == 2 ? 12 : 0);
                    Note(bass, barStart + s * step, step * 0.85f, Wave.Saw, Midi(note), 0.32f, 0.004f, step * 0.35f);
                }

                // Arpeggio: chord tones an octave up, in 16ths (8ths in the slow Hub track).
                if (style.Arpeggio)
                {
                    int steps = style.Bpm < 100f ? 8 : 16;
                    float arpStep = bar / steps;
                    for (int s = 0; s < steps; s++)
                    {
                        int note = chord[1 + s % (chord.Length - 1)] + 12 + (s / (chord.Length - 1) % 2 == 1 ? 12 : 0);
                        Note(lead, barStart + s * arpStep, arpStep * 0.7f, style.Drums ? Wave.Square : Wave.Triangle, Midi(note), 0.055f, 0.003f, arpStep * 0.4f);
                    }
                }

                if (!style.Drums)
                    continue;
                for (int beatIndex = 0; beatIndex < 4; beatIndex++)
                {
                    float t = barStart + beatIndex * beat;
                    if (style.FourOnTheFloor || beatIndex % 2 == 0)
                        Tone(drums, t, 0.3f, Wave.Sine, 130f, 42f, 0.9f, decayPower: 2.2f);
                    if (beatIndex % 2 == 1)
                    {
                        Tone(drums, t, 0.2f, Wave.Noise, 9000f, 3000f, 0.4f, decayPower: 3f, seed: 20 + b);
                        Tone(drums, t, 0.12f, Wave.Sine, 220f, 160f, 0.25f, decayPower: 2f);
                    }
                    Tone(drums, t + beat * 0.5f, 0.04f, Wave.Noise, 12000f, 9000f, 0.16f, decayPower: 4f, seed: 40 + beatIndex);
                    Tone(drums, t, 0.03f, Wave.Noise, 12000f, 9000f, 0.08f, decayPower: 4f, seed: 60 + beatIndex);
                }
            }

            LowPassLoop(bass, 900f);
            LowPassLoop(pad, style.PadCutoff);
            LowPassLoop(lead, 4500f);
            var mix = Samples(bar * style.Bars);
            Mix(mix, bass, 1f);
            Mix(mix, pad, 1f);
            Mix(mix, lead, 1f);
            Mix(mix, drums, 0.9f);
            Normalize(mix, 0.85f);
            return mix;
        }
    }
}
