using System;
using System.IO;

namespace Pathfinder.Editor.Setup
{
    /// <summary>
    /// A tiny offline synthesizer for placeholder sound effects (GDD §13.2): oscillators and noise with pitch sweeps and
    /// envelopes, a sweeping low-pass filter, and a 16-bit WAV writer. Output is deterministic, so rebuilding the content
    /// produces identical files.
    /// </summary>
    static class Synth
    {
        public const int Rate = 44100;

        public enum Wave { Sine, Square, Saw, Triangle, Noise }

        public static float[] Samples(float seconds) => new float[(int)Math.Ceiling(seconds * Rate)];

        /// <summary>
        /// Adds a voice: a wave sweeping exponentially from <paramref name="fromHz"/> to <paramref name="toHz"/>, with a
        /// short attack and a power-curve decay to silence over <paramref name="duration"/>.
        /// </summary>
        public static void Tone(float[] buffer, float start, float duration, Wave wave, float fromHz, float toHz, float volume,
            float attack = 0.004f, float decayPower = 2f, int seed = 1, float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            var random = new Random(seed);
            int first = (int)(start * Rate);
            int count = Math.Min((int)(duration * Rate), buffer.Length - first);
            double phase = 0;
            float held = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Math.Max(1, count - 1);
                double frequency = fromHz * Math.Pow(toHz / fromHz, t);
                if (vibratoDepth > 0f)
                    frequency *= 1.0 + vibratoDepth * Math.Sin(2.0 * Math.PI * vibratoHz * i / Rate);
                phase += frequency / Rate;
                double cycle = phase - Math.Floor(phase);

                float value;
                switch (wave)
                {
                    case Wave.Square: value = cycle < 0.5 ? 1f : -1f; break;
                    case Wave.Saw: value = (float)(2.0 * cycle - 1.0); break;
                    case Wave.Triangle: value = (float)(1.0 - 4.0 * Math.Abs(cycle - 0.5)); break;
                    case Wave.Noise:
                        // Sample-and-hold noise: the sweep sets how often it changes, so pitch shapes its colour.
                        if (cycle < frequency / Rate || i == 0)
                            held = (float)(random.NextDouble() * 2.0 - 1.0);
                        value = held;
                        break;
                    default: value = (float)Math.Sin(2.0 * Math.PI * cycle); break;
                }

                float envelope = Math.Min(1f, i / Math.Max(1f, attack * Rate)) * (float)Math.Pow(1.0 - t, decayPower);
                buffer[first + i] += value * envelope * volume;
            }
        }

        /// <summary>One-pole low-pass whose cutoff sweeps across the buffer: bright attacks that dull as they fade.</summary>
        public static void LowPass(float[] buffer, float fromHz, float toHz)
        {
            float state = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)Math.Max(1, buffer.Length - 1);
                double cutoff = fromHz * Math.Pow(toHz / fromHz, t);
                float alpha = (float)(1.0 - Math.Exp(-2.0 * Math.PI * cutoff / Rate));
                state += alpha * (buffer[i] - state);
                buffer[i] = state;
            }
        }

        /// <summary>Scales the buffer so its loudest sample reaches <paramref name="peak"/>.</summary>
        public static void Normalize(float[] buffer, float peak = 0.9f)
        {
            float max = 0f;
            foreach (float sample in buffer)
                max = Math.Max(max, Math.Abs(sample));
            if (max < 1e-6f)
                return;
            float gain = peak / max;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= gain;
        }

        /// <summary>Writes mono 16-bit PCM. Skips the write when the file already holds the same bytes.</summary>
        public static void WriteWav(string path, float[] buffer)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                int dataBytes = buffer.Length * 2;
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataBytes);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);       // PCM
                writer.Write((short)1);       // mono
                writer.Write(Rate);
                writer.Write(Rate * 2);       // bytes per second
                writer.Write((short)2);       // block align
                writer.Write((short)16);      // bits per sample
                writer.Write("data".ToCharArray());
                writer.Write(dataBytes);
                foreach (float sample in buffer)
                    writer.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, sample)) * short.MaxValue));
            }

            byte[] bytes = stream.ToArray();
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                return;
            File.WriteAllBytes(path, bytes);
        }
    }
}
