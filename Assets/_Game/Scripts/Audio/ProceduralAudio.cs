using System;

namespace MergeLegion.Audio
{
    /// <summary>
    /// Synthesises every sound effect and the music loops from code, so the game ships with audio and zero audio assets.
    /// Real recordings can later replace these by assigning clips in AudioManager (no gameplay code changes).
    /// </summary>
    public static class ProceduralAudio
    {
        public const int Rate = 22050;

        public enum Wave { Sine, Square, Saw, Triangle }

        public static float[] Render(SfxId id)
        {
            switch (id)
            {
                case SfxId.Click: return Blip(900f, 700f, 0.05f, Wave.Triangle, 0.35f);
                case SfxId.Buy: return Seq(Blip(520f, 520f, 0.07f, Wave.Square, 0.25f), Blip(780f, 780f, 0.1f, Wave.Square, 0.25f));
                case SfxId.Spawn: return Blip(300f, 620f, 0.12f, Wave.Sine, 0.4f);
                case SfxId.Merge: return Seq(Blip(440f, 440f, 0.07f, Wave.Triangle, 0.4f), Blip(554f, 554f, 0.07f, Wave.Triangle, 0.4f),
                    Blip(659f, 659f, 0.07f, Wave.Triangle, 0.4f), Blip(880f, 880f, 0.18f, Wave.Sine, 0.45f));
                case SfxId.Hit: return Mix(Noise(0.06f, 0.35f, 0.5f), Blip(180f, 90f, 0.07f, Wave.Square, 0.2f), 0);
                case SfxId.Death: return Mix(Noise(0.18f, 0.3f, 0.2f), Blip(300f, 70f, 0.2f, Wave.Saw, 0.2f), 0);
                case SfxId.Win: return Seq(Blip(523f, 523f, 0.12f, Wave.Square, 0.3f), Blip(659f, 659f, 0.12f, Wave.Square, 0.3f),
                    Blip(784f, 784f, 0.12f, Wave.Square, 0.3f), Blip(1047f, 1047f, 0.4f, Wave.Triangle, 0.4f));
                case SfxId.Lose: return Seq(Blip(392f, 392f, 0.18f, Wave.Triangle, 0.35f), Blip(330f, 330f, 0.18f, Wave.Triangle, 0.35f),
                    Blip(262f, 220f, 0.5f, Wave.Triangle, 0.35f));
                case SfxId.Coin: return Seq(Blip(988f, 988f, 0.06f, Wave.Square, 0.25f), Blip(1319f, 1319f, 0.16f, Wave.Square, 0.25f));
                case SfxId.Skill: return Mix(Blip(200f, 1200f, 0.35f, Wave.Saw, 0.25f), Noise(0.35f, 0.15f, 0.7f), 0);
                case SfxId.Boss: return Mix(Blip(70f, 45f, 0.8f, Wave.Saw, 0.5f), Noise(0.8f, 0.12f, 0.1f), 0);
                case SfxId.Chest: return Seq(Blip(330f, 330f, 0.1f, Wave.Square, 0.25f), Blip(415f, 415f, 0.1f, Wave.Square, 0.25f),
                    Blip(523f, 523f, 0.1f, Wave.Square, 0.25f), Blip(659f, 659f, 0.1f, Wave.Square, 0.25f), Blip(880f, 880f, 0.35f, Wave.Triangle, 0.4f));
                case SfxId.Reward: return Seq(Blip(659f, 659f, 0.09f, Wave.Triangle, 0.35f), Blip(880f, 880f, 0.09f, Wave.Triangle, 0.35f), Blip(1175f, 1175f, 0.25f, Wave.Triangle, 0.4f));
                case SfxId.Error: return Seq(Blip(180f, 180f, 0.09f, Wave.Square, 0.3f), Blip(140f, 140f, 0.14f, Wave.Square, 0.3f));
                default: return Mix(Noise(0.25f, 0.2f, 0.6f), Blip(150f, 500f, 0.25f, Wave.Sine, 0.25f), 0); // Whoosh
            }
        }

        // ---------------------------------------------------------------- building blocks

        private static float Osc(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Square: return p < 0.5 ? 1f : -1f;
                case Wave.Saw: return (float)(2.0 * p - 1.0);
                case Wave.Triangle: return (float)(4.0 * Math.Abs(p - 0.5) - 1.0);
                default: return (float)Math.Sin(p * 2.0 * Math.PI);
            }
        }

        /// <summary>Tone with a linear frequency sweep f0 -> f1 and an attack/exponential-decay envelope.</summary>
        public static float[] Blip(float f0, float f1, float seconds, Wave wave, float volume, float attack = 0.004f)
        {
            int n = Math.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                double f = f0 + (f1 - f0) * t;
                phase += f / Rate;
                float env = Math.Min(1f, i / (attack * Rate + 1f)) * (float)Math.Exp(-3.5 * t);
                data[i] = Osc(wave, phase) * env * volume;
            }
            return data;
        }

        public static float[] Noise(float seconds, float volume, float smoothing)
        {
            int n = Math.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            uint state = 0x1234ABCDu;
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                float r = (state & 0xFFFF) / 32768f - 1f;
                last = last * smoothing + r * (1f - smoothing);
                float env = (float)Math.Exp(-4.0 * i / n);
                data[i] = last * env * volume * 2f;
            }
            return data;
        }

        public static float[] Seq(params float[][] parts)
        {
            int total = 0;
            foreach (var p in parts) total += p.Length;
            var result = new float[total];
            int at = 0;
            foreach (var p in parts) { Array.Copy(p, 0, result, at, p.Length); at += p.Length; }
            return result;
        }

        public static float[] Mix(float[] a, float[] b, int offsetOfB)
        {
            int n = Math.Max(a.Length, b.Length + offsetOfB);
            var r = new float[n];
            for (int i = 0; i < a.Length; i++) r[i] += a[i];
            for (int i = 0; i < b.Length; i++) r[i + offsetOfB] += b[i];
            SoftClip(r);
            return r;
        }

        public static void SoftClip(float[] data)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (float)Math.Tanh(data[i]);
        }

        // ---------------------------------------------------------------- music

        /// <summary>
        /// A looping 8-bar track: bass + arpeggio + pad over Am-F-C-G. variant 0 = calm menu theme, 1 = faster battle theme.
        /// The loop point is seamless because every note decays inside its own step.
        /// </summary>
        public static float[] MusicLoop(int variant)
        {
            int bpm = variant == 0 ? 92 : 132;
            double beat = 60.0 / bpm;
            int bars = 8;
            int total = (int)(bars * 4 * beat * Rate);
            var data = new float[total];

            // chord roots (Hz) and thirds/fifths as semitone offsets
            float[] roots = { 110.00f, 87.31f, 130.81f, 98.00f };            // A2 F2 C3 G2
            int[][] chords = { new[] { 0, 3, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 } };

            for (int bar = 0; bar < bars; bar++)
            {
                int c = (bar / 2) % 4;
                float root = roots[c];
                double barStart = bar * 4 * beat;

                // bass on every beat (variant 1: eighth notes)
                int bassSteps = variant == 0 ? 4 : 8;
                for (int s = 0; s < bassSteps; s++)
                    AddNote(data, barStart + s * (4 * beat / bassSteps), root, (float)(4 * beat / bassSteps) * 0.9f, Wave.Triangle, 0.22f);

                // arpeggio
                int arpSteps = variant == 0 ? 8 : 16;
                for (int s = 0; s < arpSteps; s++)
                {
                    int semis = chords[c][s % 3] + (s % 6 >= 3 ? 12 : 0) + 24;
                    float freq = root * (float)Math.Pow(2.0, semis / 12.0);
                    AddNote(data, barStart + s * (4 * beat / arpSteps), freq, (float)(4 * beat / arpSteps) * 0.8f, Wave.Square, variant == 0 ? 0.05f : 0.07f);
                }

                // pad (long chord tones, once per bar)
                for (int k = 0; k < 3; k++)
                {
                    float freq = root * 2f * (float)Math.Pow(2.0, chords[c][k] / 12.0);
                    AddNote(data, barStart, freq, (float)(4 * beat) * 0.95f, Wave.Sine, 0.045f, true);
                }

                // simple percussion for the battle theme
                if (variant == 1)
                {
                    for (int b = 0; b < 4; b++)
                    {
                        double t = barStart + b * beat;
                        if (b % 2 == 0) AddKick(data, t);
                        else AddSnare(data, t);
                    }
                }
            }
            SoftClip(data);
            return data;
        }

        private static void AddNote(float[] data, double startSeconds, float freq, float length, Wave wave, float volume, bool sustain = false)
        {
            int start = (int)(startSeconds * Rate);
            int n = (int)(length * Rate);
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                int idx = start + i;
                if (idx >= data.Length) idx -= data.Length; // wrap so the tail overlaps the loop start
                phase += freq / Rate;
                float t = i / (float)n;
                float env = sustain ? (float)Math.Min(1.0, Math.Min(t * 12.0, (1.0 - t) * 6.0)) : (float)Math.Exp(-4.0 * t) * Math.Min(1f, i / 80f);
                data[idx] += Osc(wave, phase) * env * volume;
            }
        }

        private static void AddKick(float[] data, double startSeconds)
        {
            int start = (int)(startSeconds * Rate), n = (int)(0.16 * Rate);
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += (110.0 - 80.0 * t) / Rate;
                int idx = (start + i) % data.Length;
                data[idx] += (float)Math.Sin(phase * 2.0 * Math.PI) * (float)Math.Exp(-6.0 * t) * 0.35f;
            }
        }

        private static void AddSnare(float[] data, double startSeconds)
        {
            int start = (int)(startSeconds * Rate), n = (int)(0.12 * Rate);
            uint state = 0x9E3779B9u ^ (uint)start;
            for (int i = 0; i < n; i++)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                float r = (state & 0xFFFF) / 32768f - 1f;
                int idx = (start + i) % data.Length;
                data[idx] += r * (float)Math.Exp(-7.0 * i / n) * 0.18f;
            }
        }
    }
}
