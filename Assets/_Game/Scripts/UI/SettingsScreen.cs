using System;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Settings: audio, haptics, language, restore purchases, legal links, privacy choices, support, delete account/data.</summary>
    public sealed class SettingsScreen : MenuScreen
    {
        private Button _music, _sfx, _haptics;
        private TMP_Text _cloudState;

        protected override string TitleKey => "settings.title";
        protected override float RefreshSeconds => 2f;

        protected override void BuildContent(RectTransform c)
        {
            RowKit.ScrollArea(c, 12, 0, out var list);
            var legal = ServiceLocator.Get<GameConfig>().legal;

            _music = Toggle(list, "settings.music", () => Flip(s => s.musicOn = !s.musicOn, true));
            _sfx = Toggle(list, "settings.sfx", () => Flip(s => s.sfxOn = !s.sfxOn, true));
            _haptics = Toggle(list, "settings.haptics", () => Flip(s => s.hapticsOn = !s.hapticsOn, false));

            var lang = UIKit.ListRow(list, 120);
            RowKit.Left(lang, Loc.Get("settings.language"), 38, Color.white, 24, 28, 500, 64);
            RowKit.RightButton(lang, "English", UIKit.PanelLight, null, new Vector2(300, 90), 36).interactable = false;

            var cloud = UIKit.ListRow(list, 160);
            RowKit.Left(cloud, Loc.Get("settings.cloud"), 38, Color.white, 24, 14, 640, 64);
            _cloudState = RowKit.Left(cloud, "", 28, UIKit.TextDim, 24, 84, 640, 56);
            RowKit.RightButton(cloud, Loc.Get("settings.sync"), UIKit.Blue, SyncNow, new Vector2(300, 100), 36);

            Action(list, "settings.restore", UIKit.PanelLight, () =>
                ServiceLocator.Get<IapManager>().Restore((ok, n) => Toast.Show(ok ? Loc.Format("settings.restored", n) : Loc.Get("shop.purchase_failed"))));
            Action(list, "settings.privacy", UIKit.PanelLight, () => Application.OpenURL(legal.privacyUrl));
            Action(list, "settings.terms", UIKit.PanelLight, () => Application.OpenURL(legal.termsUrl));
            Action(list, "settings.privacy_choices", UIKit.PanelLight, () =>
                ServiceLocator.Get<IConsentService>().RequestConsent(_ => Toast.Show(Loc.Get("settings.saved"))));
            Action(list, "settings.support", UIKit.PanelLight, () =>
                Application.OpenURL("mailto:" + legal.supportEmail + "?subject=" + Uri.EscapeDataString(legal.supportSubject) +
                                    "&body=" + Uri.EscapeDataString("\n\n---\nID: " + ServiceLocator.Get<SaveService>().Data.playerId + "\nv" + Application.version)));
            Action(list, "settings.delete", UIKit.Bad, () =>
                Popups.Confirm(Loc.Get("settings.delete"), Loc.Get("settings.delete_msg"), Loc.Get("settings.delete_yes"), DeleteEverything, true));

            var about = UIKit.ListRow(list, 140);
            RowKit.Left(about, Loc.Get("game.title") + "  v" + Application.version, 32, UIKit.TextDim, 24, 18, 980, 50);
            RowKit.Left(about, "ID: " + ServiceLocator.Get<SaveService>().Data.playerId, 26, UIKit.TextDim, 24, 74, 980, 50);
        }

        private Button Toggle(RectTransform list, string key, UnityEngine.Events.UnityAction onClick)
        {
            var row = UIKit.ListRow(list, 120);
            RowKit.Left(row, Loc.Get(key), 38, Color.white, 24, 28, 600, 64);
            return RowKit.RightButton(row, "", UIKit.Good, onClick, new Vector2(220, 90), 40);
        }

        private static void Action(RectTransform list, string key, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var row = UIKit.ListRow(list, 120, Color.clear);
            row.GetComponent<Image>().color = Color.clear;
            var b = UIKit.Btn(row, Loc.Get(key), color, onClick, new Vector2(1000, 100), 40);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void Flip(Action<SettingsData> change, bool audio)
        {
            var save = ServiceLocator.Get<SaveService>();
            change(save.Data.settings);
            save.MarkDirty();
            save.Flush();
            if (audio && ServiceLocator.TryGet<IAudioService>(out var a)) a.ApplySettings();
        }

        private void SyncNow()
        {
            ServiceLocator.Get<CloudSyncService>().SyncOnBoot(outcome =>
            {
                Toast.Show(outcome == SyncOutcome.Offline ? Loc.Get("settings.offline") : Loc.Get("settings.synced"));
                Refresh();
            });
        }

        private void DeleteEverything()
        {
            ServiceLocator.Get<CloudSyncService>().DeleteEverything(_ =>
            {
                PopupManager.CloseAll();
                // restart from Boot so every service starts from the wiped state
                EventBus.ClearAll();
                ServiceLocator.Clear();
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Boot);
            });
        }

        protected override void Refresh()
        {
            var s = ServiceLocator.Get<SaveService>().Data.settings;
            Style(_music, s.musicOn);
            Style(_sfx, s.sfxOn);
            Style(_haptics, s.hapticsOn);
            var auth = ServiceLocator.Get<IAuthService>();
            _cloudState.text = auth.IsSignedIn ? Loc.Get("settings.signed_in") : Loc.Get("settings.offline");
        }

        private static void Style(Button b, bool on)
        {
            UIKit.SetButtonText(b, on ? Loc.Get("settings.on") : Loc.Get("settings.off"));
            b.GetComponent<Image>().color = on ? UIKit.Good : UIKit.Disabled;
        }
    }
}
