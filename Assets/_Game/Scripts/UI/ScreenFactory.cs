using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>Single place listing which screens exist in the Main scene.</summary>
    public static class ScreenFactory
    {
        public static void CreateAll(Transform root, UIManager ui)
        {
            ui.Register(UIScreen.Create<HomeScreen>(root, ScreenId.Home));
            ui.Register(UIScreen.Create<ArmyScreen>(root, ScreenId.Army));
            ui.Register(UIScreen.Create<CommandersScreen>(root, ScreenId.Commanders));
            ui.Register(UIScreen.Create<ShopScreen>(root, ScreenId.Shop));
            ui.Register(UIScreen.Create<MissionsScreen>(root, ScreenId.Missions));
            ui.Register(UIScreen.Create<BattlePassScreen>(root, ScreenId.BattlePass));
            ui.Register(UIScreen.Create<EventScreen>(root, ScreenId.Events));
            ui.Register(UIScreen.Create<ArenaScreen>(root, ScreenId.Arena));
            ui.Register(UIScreen.Create<LeaderboardScreen>(root, ScreenId.Leaderboard));
            ui.Register(UIScreen.Create<SettingsScreen>(root, ScreenId.Settings));
            ui.Register(UIScreen.Create<ProfileScreen>(root, ScreenId.Profile));
        }
    }
}
