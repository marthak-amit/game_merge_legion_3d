using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>Single place listing which screens exist in the Main scene.</summary>
    public static class ScreenFactory
    {
        public static void CreateAll(Transform root, UIManager ui)
        {
            ui.Register(UIScreen.Create<HomeScreen>(root, ScreenId.Home));
        }
    }
}
