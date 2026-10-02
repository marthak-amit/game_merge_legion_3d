namespace MergeLegion.Core
{
    public readonly struct BootCompletedEvent { }

    public readonly struct AppPauseEvent
    {
        public readonly bool Paused;
        public AppPauseEvent(bool paused) { Paused = paused; }
    }

    public readonly struct LowMemoryEvent { }

    public readonly struct SaveCompletedEvent { }

    public readonly struct ScreenChangedEvent
    {
        public readonly UI.ScreenId Screen;
        public ScreenChangedEvent(UI.ScreenId screen) { Screen = screen; }
    }
}
