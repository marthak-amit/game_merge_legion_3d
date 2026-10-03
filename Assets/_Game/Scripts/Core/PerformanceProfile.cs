using UnityEngine;

namespace MergeLegion.Core
{
    public enum DeviceTier { Low, Mid, High }

    public readonly struct PerformanceSettings
    {
        public readonly DeviceTier Tier;
        public readonly int TargetFps;
        public readonly float ResolutionScale;
        public readonly int MaxDamageNumbersPerSecond;

        public PerformanceSettings(DeviceTier tier, int fps, float scale, int numbers)
        {
            Tier = tier; TargetFps = fps; ResolutionScale = scale; MaxDamageNumbersPerSecond = numbers;
        }
    }

    /// <summary>
    /// Picks quality from device capability: 60 fps on mid-range Android (Snapdragon 6-series class), 30 fps and a lower
    /// render resolution on low-end devices. Pure function of the hardware numbers so it can be unit-tested.
    /// </summary>
    public static class PerformanceProfile
    {
        public static PerformanceSettings Choose(int systemMemoryMb, int processorCount, int processorFrequencyMhz)
        {
            if (systemMemoryMb < 3000 || processorCount < 6 || (processorFrequencyMhz > 0 && processorFrequencyMhz < 1800))
                return new PerformanceSettings(DeviceTier.Low, 30, 0.75f, 18);
            if (systemMemoryMb < 6000 || processorFrequencyMhz < 2400)
                return new PerformanceSettings(DeviceTier.Mid, 60, 0.9f, 30);
            return new PerformanceSettings(DeviceTier.High, 60, 1f, 40);
        }

        public static PerformanceSettings Detect() =>
            Choose(SystemInfo.systemMemorySize, SystemInfo.processorCount, SystemInfo.processorFrequency);

        public static void Apply(PerformanceSettings s)
        {
            Application.targetFrameRate = s.TargetFps;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = s.Tier == DeviceTier.High ? 2 : 0;
            if (s.ResolutionScale < 0.99f && Application.isMobilePlatform && !Application.isEditor)
            {
                int w = Mathf.RoundToInt(Screen.width * s.ResolutionScale), h = Mathf.RoundToInt(Screen.height * s.ResolutionScale);
                Screen.SetResolution(w, h, true);
            }
        }
    }
}
