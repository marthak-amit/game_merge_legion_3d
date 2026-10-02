using MergeLegion.Core;

namespace MergeLegion.Audio
{
    public enum SfxId { Click, Buy, Spawn, Merge, Hit, Death, Win, Lose, Coin, Skill, Boss, Chest, Reward, Error, Whoosh }

    public interface IAudioService
    {
        void PlaySfx(SfxId id, float volume = 1f);
        void PlayMusic(string id);
        void StopMusic();
        /// <summary>Re-reads the music / sfx toggles from the save.</summary>
        void ApplySettings();
    }

    public sealed class NullAudioService : IAudioService
    {
        public void PlaySfx(SfxId id, float volume = 1f) { }
        public void PlayMusic(string id) { }
        public void StopMusic() { }
        public void ApplySettings() { }
    }

    /// <summary>Static shortcut so gameplay code does not need to fetch the service.</summary>
    public static class Sfx
    {
        public static void Play(SfxId id, float volume = 1f)
        {
            if (ServiceLocator.TryGet<IAudioService>(out var audio)) audio.PlaySfx(id, volume);
        }
    }
}
