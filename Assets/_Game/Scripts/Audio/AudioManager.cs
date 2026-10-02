using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Save;
using UnityEngine;

namespace MergeLegion.Audio
{
    /// <summary>
    /// Plays procedural SFX through a small AudioSource pool plus one music source. Respects the Settings toggles.
    /// To use recorded audio instead, assign clips in <see cref="Overrides"/>; gameplay code does not change.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IAudioService
    {
        private const int PoolSize = 8;

        public Dictionary<SfxId, AudioClip> Overrides = new Dictionary<SfxId, AudioClip>();

        private readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        private readonly float[] _lastPlayed = new float[32];
        private AudioSource[] _pool;
        private AudioSource _music;
        private readonly Dictionary<string, AudioClip> _tracks = new Dictionary<string, AudioClip>();
        private string _wantedTrack;
        private int _next;

        private SaveService Save => ServiceLocator.TryGet<SaveService>(out var s) ? s : null;
        private bool SfxOn => Save == null || Save.Data.settings.sfxOn;
        private bool MusicOn => Save == null || Save.Data.settings.musicOn;

        private void Awake()
        {
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _pool[i] = gameObject.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
                _pool[i].spatialBlend = 0f;
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = 0.35f;
            EventBus.Subscribe<AppPauseEvent>(OnPause);
        }

        private void OnDestroy() => EventBus.Unsubscribe<AppPauseEvent>(OnPause);

        private void OnPause(AppPauseEvent e) => AudioListener.pause = e.Paused;

        public void PlaySfx(SfxId id, float volume = 1f)
        {
            if (!SfxOn) return;
            int slot = (int)id;
            // identical sounds closer than 45ms add nothing but loudness (e.g. 20 hits in one tick)
            if (Time.unscaledTime - _lastPlayed[slot] < 0.045f) return;
            _lastPlayed[slot] = Time.unscaledTime;

            var clip = GetClip(id);
            var src = _pool[_next];
            _next = (_next + 1) % PoolSize;
            src.PlayOneShot(clip, volume * 0.8f);
        }

        public void PlayMusic(string trackId)
        {
            _wantedTrack = trackId;
            if (!MusicOn) { _music.Stop(); return; }
            if (!_tracks.TryGetValue(trackId, out var clip))
            {
                var samples = ProceduralAudio.MusicLoop(trackId == "battle" ? 1 : 0);
                clip = AudioClip.Create("music_" + trackId, samples.Length, 1, ProceduralAudio.Rate, false);
                clip.SetData(samples, 0);
                _tracks[trackId] = clip;
            }
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            _music.Play();
        }

        public void StopMusic()
        {
            _wantedTrack = null;
            _music.Stop();
        }

        public void ApplySettings()
        {
            if (!MusicOn) _music.Stop();
            else if (_wantedTrack != null) PlayMusic(_wantedTrack);
        }

        private AudioClip GetClip(SfxId id)
        {
            if (Overrides.TryGetValue(id, out var custom) && custom != null) return custom;
            if (_clips.TryGetValue(id, out var clip)) return clip;
            var data = ProceduralAudio.Render(id);
            clip = AudioClip.Create("sfx_" + id, data.Length, 1, ProceduralAudio.Rate, false);
            clip.SetData(data, 0);
            _clips[id] = clip;
            return clip;
        }
    }
}
