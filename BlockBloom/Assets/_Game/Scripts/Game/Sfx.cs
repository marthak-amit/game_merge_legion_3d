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
        public static void Click() { Play("click", 0.5f, 1f); }
        public static void Pick() { Play("pick", 0.45f, 1f); }
        public static void Drop() { Play("drop", 0.7f, 1f); Haptic(12); }
        public static void Invalid() { Play("invalid", 0.5f, 1f); Haptic(8); }
        public static void Coin() { Play("coin", 0.6f, UnityEngine.Random.Range(0.95f, 1.15f)); }
        public static void Star(int n) { Play("star", 0.7f, 0.9f + 0.12f * n); Haptic(20); }
        public static void Win() { Play("win", 0.8f, 1f); Haptic(40); }
        public static void Lose() { Play("lose", 0.7f, 1f); Haptic(40); }
        public static void Bomb() { Play("bomb", 0.9f, 1f); Haptic(60); }
        public static void Whoosh() { Play("whoosh", 0.4f, 1f); }
        public static void Tick() { Play("tick", 0.35f, 1f); }

        /// <summary>Line clear: pitch climbs with the combo for that rising, addictive feel.</summary>
        public static void Clear(int lines, int combo)
        {
            float pitch = Mathf.Pow(1.0595f, Mathf.Min(12, (combo - 1) * 2 + (lines - 1)));
            Play("clear", Mathf.Min(1f, 0.6f + 0.1f * lines), pitch);
            if (lines >= 2) Play("chime", 0.5f, pitch);
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

        // keeps the VIBRATE permission in the manifest (Unity detects Handheld.Vibrate usage)
        #if UNITY_ANDROID && !UNITY_EDITOR
        private static void PermissionAnchor() { Handheld.Vibrate(); }
#endif

        // ---------- synthesis ----------
        private AudioClip Build(string id)
        {
            switch (id)
            {
                case "click": return Tone("click", 0.06f, t => Sine(t, 880) * Env(t, 0.06f, 0.002f));
                case "pick": return Tone("pick", 0.09f, t => Sine(t, 520 + 900 * t) * Env(t, 0.09f, 0.004f));
                case "drop": return Tone("drop", 0.14f, t => (Sine(t, 150 - 300 * t) * 0.9f + Noise(t) * 0.12f * Env(t, 0.03f, 0.001f)) * Env(t, 0.14f, 0.002f));
                case "invalid": return Tone("invalid", 0.16f, t => Square(t, 140) * 0.35f * Env(t, 0.16f, 0.004f));
                case "coin": return Tone("coin", 0.22f, t => (Sine(t, 1318) * (t < 0.07f ? 1f : 0f) + Sine(t, 1760) * (t >= 0.07f ? 1f : 0f)) * 0.7f * Env(t, 0.22f, 0.003f));
                case "tick": return Tone("tick", 0.03f, t => Sine(t, 1500) * Env(t, 0.03f, 0.001f));
                case "whoosh": return Tone("whoosh", 0.25f, t => Noise(t) * Mathf.Sin(t / 0.25f * Mathf.PI) * 0.35f);
                case "clear": return Tone("clear", 0.42f, t =>
                    {
                        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                        int n = Mathf.Min(3, (int)(t / 0.07f));
                        float lt = t - n * 0.07f;
                        return (Sine(lt, notes[n]) * 0.7f + Sine(lt, notes[n] * 2) * 0.2f) * Env(lt, 0.2f, 0.004f) * (t > 0.28f ? Mathf.Clamp01(1f - (t - 0.28f) / 0.14f) : 1f);
                    });
                case "chime": return Tone("chime", 0.5f, t => (Sine(t, 1567.98f) + 0.5f * Sine(t, 2349.3f)) * 0.4f * Env(t, 0.5f, 0.003f));
                case "star": return Tone("star", 0.5f, t => (Sine(t, 987.77f) * 0.6f + Sine(t, 1975.5f) * 0.3f) * Env(t, 0.5f, 0.004f));
                case "bomb": return Tone("bomb", 0.55f, t => (Noise(t) * 0.8f + Sine(t, 70 - 40 * t)) * Env(t, 0.55f, 0.002f) * 0.9f);
                case "win": return Tone("win", 1.2f, t =>
                    {
                        float[] n = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                        int k = Mathf.Min(4, (int)(t / 0.14f));
                        float lt = t - k * 0.14f;
                        float v = Sine(lt, n[k]) * 0.6f + Sine(lt, n[k] * 2) * 0.15f;
                        return v * Env(lt, k == 4 ? 0.6f : 0.25f, 0.005f) * (t > 0.9f ? Mathf.Clamp01(1f - (t - 0.9f) / 0.3f) : 1f);
                    });
                case "lose": return Tone("lose", 0.8f, t =>
                    {
                        float f = t < 0.2f ? 392f : (t < 0.4f ? 329.6f : 261.6f);
                        return (Sine(t, f) * 0.6f + Triangle(t, f * 0.5f) * 0.3f) * Env(t, 0.8f, 0.01f);
                    });
            }
            return null;
        }

        private static float Sine(float t, float f) { return Mathf.Sin(2f * Mathf.PI * f * t); }
        private static float Square(float t, float f) { return Mathf.Sin(2f * Mathf.PI * f * t) >= 0 ? 1f : -1f; }
        private static float Triangle(float t, float f) { float p = (t * f) % 1f; return 4f * Mathf.Abs(p - 0.5f) - 1f; }
        private static float Noise(float t) { return (float)(Hash(t) * 2.0 - 1.0); }
        private static double Hash(float t) { double x = Math.Sin(t * 12345.678) * 43758.5453; return x - Math.Floor(x); }
        private static float Env(float t, float dur, float attack)
        {
            float a = Mathf.Clamp01(t / attack);
            float d = Mathf.Clamp01(1f - t / dur);
            return a * d * d;
        }

        private static AudioClip Tone(string name, float seconds, Func<float, float> f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f) * 0.8f;
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Slow, warm 24-second loop: soft pad chords + sparse pentatonic plucks.</summary>
        private static AudioClip BuildMusic()
        {
            const float bpm = 84f;
            float beat = 60f / bpm;
            int bars = 8;
            float seconds = bars * 4 * beat;
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f },   // C
                new[] { 220.00f, 261.63f, 329.63f },   // Am
                new[] { 174.61f, 220.00f, 261.63f },   // F
                new[] { 196.00f, 246.94f, 293.66f },   // G
            };
            float[] penta = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f };
            var rng = new System.Random(7);
            for (int bar = 0; bar < bars; bar++)
            {
                var ch = chords[bar % 4];
                int s0 = (int)(bar * 4 * beat * Rate), s1 = (int)((bar + 1) * 4 * beat * Rate);
                for (int i = s0; i < s1 && i < n; i++)
                {
                    float t = (i - s0) / (float)Rate;
                    float fade = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((4 * beat - t) / 0.4f);
                    float v = 0;
                    for (int k = 0; k < ch.Length; k++) v += Mathf.Sin(2 * Mathf.PI * ch[k] * 0.5f * t) * 0.12f + Mathf.Sin(2 * Mathf.PI * ch[k] * t) * 0.05f;
                    data[i] += v * fade;
                }
                for (int b = 0; b < 8; b++)
                {
                    if (rng.NextDouble() < 0.45) continue;
                    float f = penta[rng.Next(penta.Length)];
                    int st = s0 + (int)(b * 0.5f * beat * Rate);
                    int len = (int)(0.9f * Rate);
                    for (int i = 0; i < len && st + i < n; i++)
                    {
                        float t = i / (float)Rate;
                        data[st + i] += (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * f * t)) * Mathf.Exp(-t * 4.5f) * 0.07f * Mathf.Clamp01(t / 0.004f);
                    }
                }
            }
            var c = AudioClip.Create("music", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
