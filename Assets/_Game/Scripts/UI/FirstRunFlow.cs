using System;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Save;
using MergeLegion.Services;
using TMPro;
using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>
    /// First-launch compliance flow: neutral age gate (the game is not directed at children and collects no COPPA data),
    /// then the GDPR/CCPA consent flow. iOS ATT is requested later, after the tutorial (<see cref="PermissionsFlow"/>).
    /// </summary>
    public static class FirstRunFlow
    {
        public static void Run(Action done)
        {
            var save = ServiceLocator.Get<SaveService>();
            Loc.Load(save.Data.settings.language);
            if (save.Data.consent.ageBlocked) { ShowBlocked(); return; }
            if (!save.Data.consent.ageGatePassed) AgeGate(save, () => Consent(save, done));
            else Consent(save, done);
        }

        private static void Consent(SaveService save, Action done)
        {
            if (save.Data.consent.consentAnswered) { done(); return; }
            ServiceLocator.Get<IConsentService>().RequestConsent(status =>
            {
                save.Data.consent.consentAnswered = true;
                save.Save();
                done();
            });
        }

        private static void AgeGate(SaveService save, Action passed)
        {
            int minAge = ServiceLocator.Get<GameConfig>().legal.minimumAge;
            int year = 2000;
            PopupManager.Show(() =>
            {
                var popup = PopupManager.Create(Loc.Get("age.title"), new Vector2(940, 900));
                var c = popup.Content;
                var msg = UIKit.Label(c, Loc.Get("age.message"), 38, UIKit.TextDim);
                UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(840, 150));
                var yearLabel = UIKit.Label(c, year.ToString(), 120, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(yearLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(400, 150));

                Action<int> step = delta =>
                {
                    year = Mathf.Clamp(year + delta, 1930, DateTime.Now.Year);
                    yearLabel.text = year.ToString();
                };
                var m10 = UIKit.Btn(c, "-10", UIKit.PanelLight, () => step(-10), new Vector2(130, 110), 40);
                UIKit.Place((RectTransform)m10.transform, new Vector2(0.5f, 0.5f), new Vector2(-450, 50), new Vector2(130, 110));
                var m1 = UIKit.Btn(c, "-", UIKit.PanelLight, () => step(-1), new Vector2(130, 110), 56);
                UIKit.Place((RectTransform)m1.transform, new Vector2(0.5f, 0.5f), new Vector2(-300, 50), new Vector2(130, 110));
                var p1 = UIKit.Btn(c, "+", UIKit.PanelLight, () => step(1), new Vector2(130, 110), 56);
                UIKit.Place((RectTransform)p1.transform, new Vector2(0.5f, 0.5f), new Vector2(300, 50), new Vector2(130, 110));
                var p10 = UIKit.Btn(c, "+10", UIKit.PanelLight, () => step(10), new Vector2(130, 110), 40);
                UIKit.Place((RectTransform)p10.transform, new Vector2(0.5f, 0.5f), new Vector2(450, 50), new Vector2(130, 110));

                var ok = UIKit.Btn(c, Loc.Get("common.continue"), UIKit.Good, () =>
                {
                    int age = DateTime.Now.Year - year;
                    popup.Close();
                    if (age < minAge)
                    {
                        save.Data.consent.ageBlocked = true;
                        save.Save();
                        ShowBlocked();
                        return;
                    }
                    save.Data.consent.ageGatePassed = true;
                    save.Save();
                    passed();
                }, new Vector2(700, 130), 54);
                UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(700, 130));
                return popup;
            });
        }

        private static void ShowBlocked()
        {
            PopupManager.Show(() =>
            {
                var popup = PopupManager.Create(Loc.Get("age.blocked_title"), new Vector2(940, 760));
                var msg = UIKit.Label(popup.Content, Loc.Get("age.blocked_message"), 42, Color.white);
                UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(840, 380));
                var quit = UIKit.Btn(popup.Content, Loc.Get("common.quit"), UIKit.Bad, Application.Quit, new Vector2(500, 120), 48);
                UIKit.Place((RectTransform)quit.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(500, 120));
                return popup;
            }, true);
        }
    }

    /// <summary>Asks for iOS App Tracking Transparency and notification permission once the tutorial is over.</summary>
    public static class PermissionsFlow
    {
        private static bool _subscribed;

        public static void Init()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventBus.Subscribe<Tutorial.TutorialFinishedEvent>(_ => Ask());
        }

        public static void Reset() => _subscribed = false;

        public static void AskIfDue()
        {
            var save = ServiceLocator.Get<SaveService>();
            if (save.Data.HasFlag(SaveFlags.TutorialDone) && !save.Data.consent.trackingPromptShown) Ask();
        }

        private static void Ask()
        {
            if (!ServiceLocator.TryGet<SaveService>(out var save) || save.Data.consent.trackingPromptShown) return;
            PopupManager.Show(() =>
            {
                var popup = PopupManager.Create(Loc.Get("perm.title"), new Vector2(940, 800));
                var msg = UIKit.Label(popup.Content, Loc.Get("perm.message"), 40, Color.white);
                UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(840, 400));
                var ok = UIKit.Btn(popup.Content, Loc.Get("common.continue"), UIKit.Good, () =>
                {
                    popup.Close();
                    save.Data.consent.trackingPromptShown = true;
                    save.MarkDirty();
                    ServiceLocator.Get<IConsentService>().RequestTracking(_ => { });
                    ServiceLocator.Get<IPushService>().RequestPermission(_ => { });
                }, new Vector2(700, 130), 54);
                UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(700, 130));
                return popup;
            });
        }
    }
}
