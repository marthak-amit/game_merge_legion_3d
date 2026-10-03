using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBloom
{
    /// <summary>Procedural sound effects + a gentle generative music loop (no audio files shipped), and short haptic taps.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        private static Sfx _i;
        private AudioSource _music;
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private int _next;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private const int Rate = 32000;

        public static void Init()
        {
            if (_i != null) return;
            var go = new GameObject("[Sfx]");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<Sfx>();
            _i.Setup();
        }

        private void Setup()
        {
            for (int i = 0; i < 8; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                _voices.Add(s);
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true; _music.playOnAwake = false; _music.volume = 0.22f; _music.spatialBlend = 0f;
            _music.clip = BuildMusic();
            ApplyMusicSetting();
        }

        public static void ApplyMusicSetting()
        {
            if (_i == null || _i._music == null) return;
            if (Save.Data != null && Save.Data.musicOn) { if (!_i._music.isPlaying) _i._music.Play(); }
            else _i._music.Pause();
        }

        // ---------- public cues ----------
        public static void Click() { Play("click", 0.55f, UnityEngine.Random.Range(0.97f, 1.04f)); }
        public static void Pick() { Play("pick", 0.5f, 1f); }
        public static void Drop() { Play("drop", 0.8f, UnityEngine.Random.Range(0.96f, 1.04f)); Haptic(12); }
        public static void Invalid() { Play("invalid", 0.55f, 1f); Haptic(8); }
        public static void Coin() { Play("coin", 0.6f, UnityEngine.Random.Range(0.96f, 1.12f)); }
        public static void Reward() { Play("reward", 0.7f, 1f); Haptic(25); }
        public static void Pop() { Play("pop", 0.5f, UnityEngine.Random.Range(0.97f, 1.05f)); }
        public static void Star(int n) { Play("star" + Mathf.Clamp(n, 1, 3), 0.75f, 1f); Haptic(20); }
        public static void Win() { Play("win", 0.85f, 1f); Haptic(40); }
        public static void Lose() { Play("lose", 0.7f, 1f); Haptic(40); }
        public static void Bomb() { Play("bomb", 0.95f, 1f); Haptic(60); }
        public static void Whoosh() { Play("whoosh", 0.4f, UnityEngine.Random.Range(0.95f, 1.05f)); }
        public static void Tick() { Play("tick", 0.3f, UnityEngine.Random.Range(0.97f, 1.08f)); }
        public static void Warn() { Play("warn", 0.5f, 1f); Haptic(15); }
        public static void Break() { Play("heartbreak", 0.7f, 1f); Haptic(35); }

        /// <summary>Line clear: a bell arpeggio that climbs the scale with the combo - the rising pitch is what makes streaks addictive.</summary>
        public static void Clear(int lines, int combo)
        {
            Play("clear_" + Mathf.Clamp(lines, 1, 5) + "_" + Mathf.Clamp(combo, 1, 7), Mathf.Min(1f, 0.65f + 0.08f * lines), 1f);
            Haptic(lines >= 3 ? 45 : 25);
        }

        private static void Play(string id, float vol, float pitch)
        {
            if (_i == null || Save.Data == null || !Save.Data.sfxOn) return;
            AudioClip c;
            if (!_i._clips.TryGetValue(id, out c)) { c = _i.Build(id); _i._clips[id] = c; }
            if (c == null) return;
            var v = _i._voices[_i._next]; _i._next = (_i._next + 1) % _i._voices.Count;
            v.pitch = pitch; v.volume = vol; v.clip = c; v.Play();
        }

        public static void Haptic(int ms)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Save.Data == null || !Save.Data.hapticsOn) return;
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vib != null) vib.Call("vibrate", (long)ms);
                }
            }
            catch (Exception) { }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // keeps the VIBRATE permission in the manifest (Unity detects Handheld.Vibrate usage)
        private static void PermissionAnchor() { Handheld.Vibrate(); }
#endif

        // ---------- synthesis ----------
        private const float C5 = 523.25f;
        private static readonly int[] Scale = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24, 26, 28 };   // major pentatonic over 2+ octaves
        private static float Note(int semis) { return C5 * Mathf.Pow(2f, semis / 12f); }

        private static float[] Buf(float seconds) { return new float[(int)(seconds * Rate)]; }

        private static uint _seed = 12345;
        private static float Rnd() { _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5; return (_seed & 0xFFFFFF) / 8388608f - 1f; }

        /// <summary>Mixes a voice (function of seconds since its start) into a buffer.</summary>
        private static void Add(float[] b, float start, float dur, float gain, Func<float, float> f)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n && s0 + i < b.Length; i++) b[s0 + i] += f(i / (float)Rate) * gain;
        }

        private static float Bell(float t, float f, float decay)
        {
            float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(t / 0.003f);
            float w = Mathf.Sin(6.2831853f * f * t) + 0.42f * Mathf.Sin(6.2831853f * f * 2.76f * t) * Mathf.Exp(-decay * 1.6f * t)
                      + 0.18f * Mathf.Sin(6.2831853f * f * 5.4f * t) * Mathf.Exp(-decay * 2.5f * t);
            return w * env;
        }

        private static float Pluck(float t, float f, float decay)
        {
            float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(t / 0.004f);
            return (Mathf.Sin(6.2831853f * f * t) + 0.35f * Mathf.Sin(12.566371f * f * t) + 0.12f * Mathf.Sin(18.849556f * f * t)) * env;
        }

        private static void Echo(float[] b, float delay, float feedback, float mix)
        {
            int d = (int)(delay * Rate);
            for (int i = d; i < b.Length; i++) b[i] += b[i - d] * feedback * mix;
        }

        private static AudioClip Finish(string name, float[] b, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < b.Length; i++) { float a = Mathf.Abs(b[i]); if (a > max) max = a; }
            float k = peak / max;
            int fade = Mathf.Min(b.Length / 8, 400);
            for (int i = 0; i < b.Length; i++)
            {
                b[i] *= k;
                if (i > b.Length - fade) b[i] *= (b.Length - i) / (float)fade;
            }
            var c = AudioClip.Create(name, b.Length, 1, Rate, false);
            c.SetData(b, 0);
            return c;
        }

        private AudioClip Build(string id)
        {
            float[] b;
            if (id.StartsWith("clear_"))
            {
                var parts = id.Split('_');
                int lines = int.Parse(parts[1]), combo = int.Parse(parts[2]);
                b = Buf(1.1f);
                int notes = 2 + lines;
                int start = Mathf.Min(Scale.Length - notes - 1, (combo - 1) * 1);
                if (start < 0) start = 0;
                for (int k = 0; k < notes; k++)
                {
                    float f = Note(Scale[Mathf.Min(Scale.Length - 1, start + k)]);
                    Add(b, k * 0.052f, 0.7f, 0.6f, t => Bell(t, f, 6.5f));
                }
                Add(b, 0, 0.18f, 0.5f, t => Mathf.Sin(6.2831853f * (140f - 300f * t) * t) * Mathf.Exp(-16f * t));
                if (lines >= 3) Add(b, notes * 0.052f, 0.9f, 0.35f, t => Bell(t, Note(Scale[Mathf.Min(Scale.Length - 1, start + notes)] + 12), 4f));
                Echo(b, 0.11f, 0.5f, 0.6f);
                return Finish(id, b, 0.85f);
            }
            switch (id)
            {
                case "click":
                    b = Buf(0.09f);
                    Add(b, 0, 0.09f, 1f, t => Mathf.Sin(6.2831853f * (620f - 2200f * t) * t) * Mathf.Exp(-45f * t) + Rnd() * 0.15f * Mathf.Exp(-300f * t));
                    return Finish(id, b, 0.7f);
                case "pick":
                    b = Buf(0.16f);
                    Add(b, 0, 0.16f, 1f, t => Bell(t, 780f + 1200f * t, 22f));
                    return Finish(id, b, 0.6f);
                case "drop":
                    b = Buf(0.22f);
                    Add(b, 0, 0.22f, 1f, t => Mathf.Sin(6.2831853f * (170f - 600f * t) * t) * Mathf.Exp(-22f * t));
                    Add(b, 0, 0.05f, 0.5f, t => Rnd() * Mathf.Exp(-120f * t));
                    Add(b, 0.012f, 0.1f, 0.35f, t => Bell(t, 1040f, 38f));
                    return Finish(id, b, 0.9f);
                case "invalid":
                    b = Buf(0.3f);
                    Add(b, 0, 0.14f, 0.8f, t => Mathf.Sin(6.2831853f * 300f * t) * Mathf.Exp(-14f * t));
                    Add(b, 0.11f, 0.19f, 0.8f, t => Mathf.Sin(6.2831853f * 220f * t) * Mathf.Exp(-12f * t));
                    return Finish(id, b, 0.6f);
                case "coin":
                    b = Buf(0.7f);
                    Add(b, 0, 0.5f, 0.8f, t => Bell(t, 1318.5f, 9f));
                    Add(b, 0.075f, 0.6f, 0.9f, t => Bell(t, 1760f, 8f));
                    Echo(b, 0.09f, 0.4f, 0.5f);
                    return Finish(id, b, 0.7f);
                case "reward":
                    b = Buf(1.3f);
                    for (int k = 0; k < 8; k++) { float f = Note(Scale[3 + k % 7] ); Add(b, k * 0.055f, 0.5f, 0.55f, t => Bell(t, f, 7f)); }
                    Add(b, 0.5f, 0.8f, 0.5f, t => Bell(t, Note(24), 4.5f));
                    Echo(b, 0.12f, 0.5f, 0.6f);
                    return Finish(id, b, 0.8f);
                case "pop":
                    b = Buf(0.22f);
                    Add(b, 0, 0.22f, 1f, t => Mathf.Sin(6.2831853f * (420f + 1600f * t) * t) * Mathf.Exp(-24f * t));
                    Add(b, 0.03f, 0.2f, 0.4f, t => Bell(t, 1320f, 18f));
                    return Finish(id, b, 0.6f);
                case "tick":
                    b = Buf(0.04f);
                    Add(b, 0, 0.04f, 1f, t => Mathf.Sin(6.2831853f * 2000f * t) * Mathf.Exp(-110f * t));
                    return Finish(id, b, 0.5f);
                case "warn":
                    b = Buf(0.35f);
                    Add(b, 0, 0.12f, 0.8f, t => Mathf.Sin(6.2831853f * 460f * t) * Mathf.Exp(-18f * t));
                    Add(b, 0.14f, 0.2f, 0.8f, t => Mathf.Sin(6.2831853f * 460f * t) * Mathf.Exp(-18f * t));
                    return Finish(id, b, 0.55f);
                case "whoosh":
                    b = Buf(0.38f);
                    {
                        float lp = 0f;
                        int n = b.Length;
                        for (int i = 0; i < n; i++)
                        {
                            float tt = i / (float)n;
                            float cut = 0.04f + 0.45f * Mathf.Sin(tt * Mathf.PI);
                            lp += (Rnd() - lp) * cut;
                            b[i] = lp * Mathf.Sin(tt * Mathf.PI) * 1.4f;
                        }
                    }
                    return Finish(id, b, 0.45f);
                case "star1": case "star2": case "star3":
                    {
                        int n = int.Parse(id.Substring(4));
                        b = Buf(1.0f);
                        int baseIdx = 3 + n;
                        for (int k = 0; k < 3; k++) { float f = Note(Scale[baseIdx + k]); Add(b, k * 0.06f, 0.8f, 0.6f, t => Bell(t, f, 5.5f)); }
                        Add(b, 0.18f, 0.8f, 0.35f, t => Bell(t, Note(Scale[baseIdx + 3]), 4.5f));
                        Echo(b, 0.13f, 0.5f, 0.6f);
                        return Finish(id, b, 0.8f);
                    }
                case "bomb":
                    b = Buf(0.85f);
                    {
                        float lp = 0f;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = i / (float)Rate;
                            float cut = Mathf.Max(0.02f, 0.5f * Mathf.Exp(-5f * t));
                            lp += (Rnd() - lp) * cut;
                            b[i] = lp * Mathf.Exp(-4f * t) * 1.6f + Mathf.Sin(6.2831853f * (70f - 30f * t) * t) * Mathf.Exp(-6f * t);
                        }
                    }
                    return Finish(id, b, 0.95f);
                case "win":
                    b = Buf(2.2f);
                    {
                        int[] mel = { 0, 4, 7, 12, 16, 19, 24 };
                        for (int k = 0; k < mel.Length; k++) { float f = Note(mel[k]); float dur = k == mel.Length - 1 ? 1.4f : 0.5f; Add(b, k * 0.11f, dur, 0.55f, t => Bell(t, f, k == mel.Length - 1 ? 2.8f : 6f)); }
                        float[] chord = { Note(0), Note(4), Note(7), Note(12) };
                        for (int k = 0; k < chord.Length; k++) { float f = chord[k]; Add(b, 0.77f, 1.4f, 0.22f, t => (Mathf.Sin(6.2831853f * f * t) + 0.3f * Mathf.Sin(12.566371f * f * t)) * Mathf.Exp(-1.8f * t) * Mathf.Clamp01(t / 0.02f)); }
                        Echo(b, 0.16f, 0.5f, 0.55f);
                    }
                    return Finish(id, b, 0.85f);
                case "lose":
                    b = Buf(1.3f);
                    {
                        int[] mel = { -5, -9, -12 };
                        for (int k = 0; k < mel.Length; k++) { float f = Note(mel[k]); Add(b, k * 0.22f, 0.9f, 0.7f, t => Pluck(t, f, 3.2f)); }
                        Echo(b, 0.15f, 0.4f, 0.5f);
                    }
                    return Finish(id, b, 0.7f);
                case "heartbreak":
                    b = Buf(0.7f);
                    Add(b, 0, 0.5f, 0.8f, t => Pluck(t, 440f, 5f));
                    Add(b, 0.14f, 0.55f, 0.8f, t => Pluck(t, 311f, 4.5f));
                    return Finish(id, b, 0.65f);
            }
            return null;
        }

        /// <summary>Warm 24 s loop: soft pad chords, a gentle bass, bell arpeggios and a sparse pentatonic melody with echo.</summary>
        private static AudioClip BuildMusic()
        {
            const float bpm = 88f;
            float beat = 60f / bpm;
            int bars = 8;
            var b = Buf(bars * 4 * beat);
            int[][] chords = { new[] { 0, 4, 7 }, new[] { -3, 0, 4 }, new[] { -7, -3, 0 }, new[] { -5, -1, 2 } };   // C Am F G (relative to C5)
            int[] roots = { -12, -15, -19, -17 };
            var rng = new System.Random(11);
            for (int bar = 0; bar < bars; bar++)
            {
                int[] ch = chords[bar % 4];
                float t0 = bar * 4 * beat;
                for (int k = 0; k < ch.Length; k++)
                {
                    float f = Note(ch[k] - 12);
                    Add(b, t0, 4 * beat + 0.3f, 0.10f, t => (Mathf.Sin(6.2831853f * f * t) + 0.25f * Mathf.Sin(12.566371f * f * t)) * Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((4 * beat + 0.3f - t) / 0.6f));
                }
                for (int k = 0; k < 2; k++)
                {
                    float f = Note(roots[bar % 4]);
                    Add(b, t0 + k * 2 * beat, 1.2f, 0.28f, t => Mathf.Sin(6.2831853f * f * t) * Mathf.Exp(-2.6f * t) * Mathf.Clamp01(t / 0.01f));
                }
                for (int e = 0; e < 8; e++)
                {
                    float f = Note(ch[e % 3] + (e % 6 >= 3 ? 12 : 0));
                    Add(b, t0 + e * 0.5f * beat, 0.7f, 0.11f, t => Bell(t, f, 7f));
                }
                for (int e = 0; e < 4; e++)
                {
                    if (rng.NextDouble() < 0.5) continue;
                    float f = Note(Scale[3 + rng.Next(6)] + 12);
                    Add(b, t0 + e * beat + (rng.NextDouble() < 0.5 ? 0.5f * beat : 0f), 1.2f, 0.17f, t => Bell(t, f, 3.8f));
                }
            }
            Echo(b, beat * 0.75f, 0.45f, 0.5f);
            return Finish("music", b, 0.5f);
        }
    }
}
