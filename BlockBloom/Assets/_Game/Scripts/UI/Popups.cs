using System;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;

namespace BlockBloom
{
    /// <summary>All the meta pop-ups: settings, shop, daily reward, lucky spin, quests, themes, hearts, booster offers.</summary>
    public static class Popups
    {
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        // ---------- settings ----------
        public static void Settings(Action onChanged)
        {
            var p = Popup.Create(new Vector2(920, 1400), "SETTINGS", true, Palette.Purple);
            float y = 470;
            Toggle(p.Card, "SOUND FX", Sprites.Speaker(), y, () => Save.Data.sfxOn, v => { Save.Data.sfxOn = v; Save.Commit(); });
            Toggle(p.Card, "MUSIC", Sprites.Note(), y - 125, () => Save.Data.musicOn, v => { Save.Data.musicOn = v; Save.Commit(); Sfx.ApplyMusicSetting(); });
            Toggle(p.Card, "VIBRATION", Sprites.Pause(), y - 250, () => Save.Data.hapticsOn, v => { Save.Data.hapticsOn = v; Save.Commit(); Sfx.Haptic(30); });
            Toggle(p.Card, "PERSONALIZED ADS", Sprites.Star(), y - 375, () => Save.Data.adsPersonalised && !Save.Data.isUnder13,
                v => { Save.Data.adsPersonalised = v; Save.Commit(); if (v && Save.Data.isUnder13) App.I.Toast("Not available for players under 13"); });
            Ui.Btn(p.Card, "THEMES", Palette.Hex("#ff5a96"), Palette.Hex("#b82d62"), new Vector2(660, 96), () => Themes(null), 44).Pos(0, -30);
            Ui.Btn(p.Card, "RESTORE PURCHASES", Palette.Blue, Palette.BlueDark, new Vector2(660, 96), () =>
            {
                var iap = Monet.Iap; if (iap != null) iap.RestorePurchases(ok => App.I.Toast(ok ? "Purchases restored" : "Nothing to restore"));
            }, 40).Pos(0, -150);
            Ui.Btn(p.Card, "PRIVACY", Palette.Hex("#7a76a8"), Palette.Hex("#4c4880"), new Vector2(320, 90), () => Application.OpenURL(Links.PrivacyPolicy), 38).Pos(-170, -265);
            Ui.Btn(p.Card, "TERMS", Palette.Hex("#7a76a8"), Palette.Hex("#4c4880"), new Vector2(320, 90), () => Application.OpenURL(Links.Terms), 38).Pos(170, -265);
            Ui.Btn(p.Card, "SUPPORT", Palette.Hex("#7a76a8"), Palette.Hex("#4c4880"), new Vector2(320, 90), () => Application.OpenURL(Links.Support), 38).Pos(-170, -375);
            Ui.Btn(p.Card, "RESET PROGRESS", Palette.Red, Palette.RedDark, new Vector2(320, 90), () =>
            {
                Confirm("Erase all progress?", "This cannot be undone.", "ERASE", () => { Save.Wipe(); p.Close(true); App.I.ShowHome(); });
            }, 30).Pos(170, -375);
            Ui.Label(p.Card, "Block Bloom  v" + Application.version, 28, Palette.Alpha(Color.white, 0.6f), TextAnchor.MiddleCenter, false).Pos(0, -490);
            p.OnClosed = onChanged;
        }

        // ---------- first launch: age screen (COPPA) + privacy notice (CCPA) ----------
        public static void AgeGate(Action done)
        {
            var p = Popup.Create(new Vector2(920, 1060), "WELCOME!", false, Palette.Green);
            Ui.Label(p.Card, "Quick question before you play", 44, Color.white, TextAnchor.MiddleCenter).Pos(0, 330);
            var hero = Ui.Img(p.Card, Icons.Gift(), Color.white, "hero"); hero.rectTransform.sizeDelta = new Vector2(190, 190); hero.Pos(0, 170);
            Anim.Wiggle(hero.transform, 8f, 0.8f);
            var t = Ui.Label(p.Card, "How old are you?\nThis helps us keep ads and purchases age-appropriate.", 36, Palette.Alpha(Color.white, 0.9f), TextAnchor.MiddleCenter, false);
            t.Pos(0, -10).Size(760, 140); t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Btn(p.Card, "I'M 13 OR OLDER", Palette.Green, Palette.GreenDark, new Vector2(740, 130), () =>
            {
                Save.Data.ageGateDone = true; Save.Data.isUnder13 = false; Save.Commit(); p.Close(true); if (done != null) done();
            }, 54).Pos(0, -170);
            Ui.Btn(p.Card, "I'M UNDER 13", Palette.Blue, Palette.BlueDark, new Vector2(740, 110), () =>
            {
                Save.Data.ageGateDone = true; Save.Data.isUnder13 = true; Save.Data.adsPersonalised = false; Save.Commit(); p.Close(true); if (done != null) done();
            }, 46).Pos(0, -320);
            var pp = Ui.Label(p.Card, "By continuing you agree to our Terms and Privacy Policy.", 28, Palette.Alpha(Color.white, 0.65f), TextAnchor.MiddleCenter, false);
            pp.Pos(0, -430).Size(800, 50);
            Ui.Btn(p.Card, "PRIVACY POLICY", Palette.Hex("#7a76a8"), Palette.Hex("#4c4880"), new Vector2(340, 70), () => Application.OpenURL(Links.PrivacyPolicy), 28).Pos(-190, -490);
            Ui.Btn(p.Card, "TERMS", Palette.Hex("#7a76a8"), Palette.Hex("#4c4880"), new Vector2(340, 70), () => Application.OpenURL(Links.Terms), 28).Pos(190, -490);
        }

        // ---------- rate us (asked once, after the player has had real fun) ----------
        public static void RatePrompt()
        {
            if (Save.Data.ratePromptShown) return;
            Save.Data.ratePromptShown = true; Save.Commit();
            var p = Popup.Create(new Vector2(880, 800), "ENJOYING BLOCK BLOOM?", true, Palette.Hex("#ff5a96"));
            var stars = Widgets.Stars(p.Card, 3, 120, 24); stars.Pos(0, 200);
            Ui.Label(p.Card, "A quick rating helps us a lot!", 42, Color.white, TextAnchor.MiddleCenter).Pos(0, 40);
            Ui.Btn(p.Card, "RATE 5 STARS", Palette.Green, Palette.GreenDark, new Vector2(640, 120), () =>
            {
                Application.OpenURL(Application.platform == RuntimePlatform.IPhonePlayer ? Links.IosStore : Links.AndroidStore);
                Monet.Log("rate_yes"); p.Close();
            }, 52).Pos(0, -120);
            Ui.Btn(p.Card, "NOT NOW", Palette.Hex("#7a76a8"), Palette.Hex("#4c4880"), new Vector2(400, 90), () => { Monet.Log("rate_later"); p.Close(); }, 38).Pos(0, -270);
        }

        private static void Toggle(RectTransform card, string label, Sprite icon, float y, Func<bool> get, Action<bool> set)
        {
            var row = Ui.RoundImg(card, new Color(0.05f, 0.03f, 0.2f, 0.45f), 40, "row");
            row.rectTransform.sizeDelta = new Vector2(740, 120); row.Pos(0, y);
            var ic = Ui.Img(row.transform, icon, Color.white, "icon");
            Ui.At(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(72, 72));
            var t = Ui.Label(row.transform, label, 46, Color.white, TextAnchor.MiddleLeft);
            Ui.At(t.rectTransform, new Vector2(0, 0.5f), new Vector2(330, 0), new Vector2(420, 80));
            Button btn = null;
            Action refresh = () =>
            {
                bool on = get();
                var tx = Ui.ButtonText(btn); tx.text = on ? "ON" : "OFF";
                var faces = btn.transform.Find("face").GetComponent<Image>();
                faces.color = on ? Palette.Green : Palette.Hex("#7a76a8");
                btn.transform.Find("lip").GetComponent<Image>().color = on ? Palette.GreenDark : Palette.Hex("#4c4880");
            };
            btn = Ui.Btn(row.transform, "ON", Palette.Green, Palette.GreenDark, new Vector2(190, 84), () => { set(!get()); refresh(); }, 40);
            Ui.At((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(-125, 4), new Vector2(190, 84));
            refresh();
        }

        // ---------- generic confirm ----------
        public static void Confirm(string title, string text, string yes, Action onYes)
        {
            var p = Popup.Create(new Vector2(840, 620), title, true, Palette.Red);
            var t = Ui.Label(p.Card, text, 42, Color.white, TextAnchor.MiddleCenter, false);
            t.Pos(0, 60).Size(680, 160);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Btn(p.Card, yes, Palette.Red, Palette.RedDark, new Vector2(520, 110), () => { p.Close(); onYes(); }, 50).Pos(0, -150);
        }

        // ---------- shop ----------
        public static void Shop()
        {
            var p = Popup.Create(new Vector2(960, 1560), "SHOP", true, Palette.Green);
            var top = CoinPill.Create(p.Card, false, null);
            Ui.At((RectTransform)top.transform, new Vector2(0.5f, 1f), new Vector2(0, -196), new Vector2(330, 84));
            top.gameObject.AddComponent<NoStagger>();

            float y = 470;
            var starter = Economy.Products[0];
            if (!Save.Data.starterBought) { Banner(p, starter, y, Palette.Hex("#ff9f1c"), Palette.Hex("#c06a00"), "800 coins + 3 of every booster + full hearts", Icons.Gift()); y -= 190; }
            var noads = Economy.Products[1];
            if (!Save.Data.adsRemoved) { Banner(p, noads, y, Palette.Blue, Palette.BlueDark, "No more pop-up ads. Rewarded videos stay optional.", Icons.Video()); y -= 190; }

            float tileY = y - 130 + 20;
            for (int i = 2; i < Economy.Products.Length; i++)
            {
                var prod = Economy.Products[i];
                int k = i - 2;
                Tile(p, prod, (k % 2 == 0) ? -230 : 230, tileY - (k / 2) * 330);
            }

            var free = Ui.Btn(p.Card, "", Palette.Green, Palette.GreenDark, new Vector2(780, 120), () =>
            {
                Monet.Rewarded("shop_free", () => { Economy.AddCoins(50); Sfx.Reward(); App.I.Toast("+50 coins!"); });
            }, 42);
            free.Pos(0, -650);
            var vi = Ui.Img(free.transform, Icons.Video(), Color.white, "vi"); Ui.At(vi.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(84, 84));
            Ui.LabelAt(free.transform, "FREE +50 COINS", 50, Color.white, new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(560, 80));
            Anim.AddShine(free);
        }

        private static void Banner(Popup p, Economy.Product prod, float y, Color face, Color lip, string sub, Sprite icon)
        {
            Button b = null;
            b = Ui.Btn(p.Card, "", face, lip, new Vector2(820, 160), () => Monet.Buy(prod, () =>
            {
                Sfx.Reward();
                if (Fx.I != null) { Fx.I.Burst(b.transform.position, Palette.Gold, 26, 700f, 34f); Fx.I.Float(b.transform.position, "PURCHASED!", 80, Palette.Gold); }
                b.gameObject.SetActive(false);
            }), 40);
            b.transform.localPosition = new Vector3(0, y, 0);
            var ic = Ui.Img(b.transform, icon, Color.white, "icon"); Ui.At(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(86, 0), new Vector2(110, 110));
            Anim.Wiggle(ic.transform, 10f, 0.9f);
            var t = Ui.Label(b.transform, prod.Title, 54, Color.white, TextAnchor.MiddleLeft);
            Ui.At(t.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 34), new Vector2(380, 70)); t.rectTransform.pivot = new Vector2(0, 0.5f);
            var s = Ui.Label(b.transform, sub, 26, Palette.Alpha(Color.white, 0.92f), TextAnchor.MiddleLeft, false);
            Ui.At(s.rectTransform, new Vector2(0, 0.5f), new Vector2(160, -28), new Vector2(380, 70)); s.rectTransform.pivot = new Vector2(0, 0.5f);
            s.horizontalOverflow = HorizontalWrapMode.Wrap;
            var price = Widgets.Pill(b.transform, Monet.PriceText(prod), Palette.Alpha(Palette.Ink, 0.75f), new Vector2(190, 84), 44);
            Ui.At(price.transform.parent.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-120, 0), new Vector2(190, 84));
            Anim.AddShine(b, 4f);
        }

        private static void Tile(Popup p, Economy.Product prod, float x, float y)
        {
            Button b = null;
            b = Ui.Btn(p.Card, "", Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), new Vector2(420, 290), () => Monet.Buy(prod, () =>
            {
                Sfx.Reward();
                if (Fx.I != null) { Fx.I.Burst(b.transform.position, Palette.Gold, 26, 700f, 34f); Fx.I.Float(b.transform.position, "+" + Ui.Num(prod.Coins), 90, Palette.Gold); }
            }), 40);
            b.transform.localPosition = new Vector3(x, y, 0);
            var t = Ui.Label(b.transform, prod.Title, 38, Palette.Hex("#ffe9a8"), TextAnchor.MiddleCenter);
            Ui.At(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(380, 60));
            int n = Mathf.Clamp(prod.Coins / 1500 + 1, 1, 4);
            for (int i = 0; i < n; i++)
            {
                var c = Ui.Img(b.transform, Sprites.Coin(), Palette.Gold, "c");
                Ui.At(c.rectTransform, C, new Vector2((i - (n - 1) * 0.5f) * 56, 30), new Vector2(92, 92));
            }
            var amt = Ui.Label(b.transform, Ui.Num(prod.Coins), 52, Color.white, TextAnchor.MiddleCenter);
            Ui.At(amt.rectTransform, C, new Vector2(0, -34), new Vector2(380, 70));
            var price = Widgets.Pill(b.transform, Monet.PriceText(prod), Palette.Green, new Vector2(240, 66), 40);
            Ui.At(price.transform.parent.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(240, 66));
        }

        // ---------- daily login ----------
        public static void DailyReward(Action onClaimed)
        {
            var p = Popup.Create(new Vector2(960, 1100), "DAILY REWARD", true, Palette.Hex("#ff7a3d"));
            int today = Economy.NextLoginDay;
            bool can = Economy.LoginAvailable;
            for (int i = 0; i < 7; i++)
            {
                int col = i % 4, row = i / 4;
                bool big = i == 6;
                float w = big ? 380 : 190;
                float x = big ? 0 : (col - 1.5f) * 200;
                float yy = 290 - row * 280;
                if (big) { x = 0; yy = 20; }
                bool done = can ? i < today : i < Save.Data.streak;
                bool isToday = can && i == today;
                Color face = done ? Palette.Hex("#3b8f5b") : (isToday ? Palette.Gold : Palette.Hex("#6f4bd8"));
                Color lip = done ? Palette.Hex("#1f6a3b") : (isToday ? Palette.GoldDark : Palette.Hex("#45299c"));
                var cell = Ui.Rect(p.Card, "day" + i);
                cell.sizeDelta = new Vector2(w, big ? 200 : 240);
                if (big) { yy = -150; }
                cell.localPosition = new Vector3(x, yy, 0);
                var lipI = Ui.Img(cell, Sprites.Glossy(28), lip, "lip");
                Ui.Stretch(lipI.rectTransform); lipI.rectTransform.offsetMin = new Vector2(0, -9); lipI.rectTransform.offsetMax = new Vector2(0, -9);
                var f = Ui.Img(cell, Sprites.Glossy(28), face, "face"); Ui.Stretch(f.rectTransform);
                var d = Ui.Label(cell, "DAY " + (i + 1), 34, Color.white, TextAnchor.MiddleCenter);
                Ui.At(d.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(w, 50));
                var ic = Ui.Img(cell, done ? Sprites.Check() : Sprites.Coin(), done ? Color.white : Palette.Gold, "ic");
                Ui.At(ic.rectTransform, C, new Vector2(0, big ? 20 : 8), new Vector2(big ? 110 : 84, big ? 110 : 84));
                var amt = Ui.Label(cell, Economy.LoginRewards[i] + (big ? " + BOOSTERS" : ""), big ? 40 : 38, Color.white, TextAnchor.MiddleCenter);
                Ui.At(amt.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 38), new Vector2(w, 54));
                if (isToday) Ui.Later(0.9f, () => Pulse(cell));
            }
            if (can)
            {
                Ui.Btn(p.Card, "CLAIM", Palette.Green, Palette.GreenDark, new Vector2(520, 120), () =>
                {
                    int r = Economy.ClaimLogin();
                    Sfx.Reward(); Widgets.CoinBurst(p.Card.position, 14);
                    App.I.Toast("+" + r + " coins!");
                    if (onClaimed != null) onClaimed();
                    p.Close();
                }, 62).Pos(0, -400);
            }
            else Ui.Label(p.Card, "Come back tomorrow!", 46, Color.white, TextAnchor.MiddleCenter).Pos(0, -400);
        }

        private static void Pulse(RectTransform rt)
        {
            Tween.Value(0.9f, k => { if (rt != null) rt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(k * Mathf.PI * 2f)); }, Ease.Linear, () => { if (rt != null) Pulse(rt); }, 0f, rt);
        }

        // ---------- lucky spin ----------
        public static void Spin(Action onDone)
        {
            var p = Popup.Create(new Vector2(960, 1260), "LUCKY SPIN", true, Palette.Purple);
            var wheel = Ui.Rect(p.Card, "wheel"); wheel.sizeDelta = new Vector2(700, 700); wheel.localPosition = new Vector3(0, 90, 0);
            var rim = Ui.Img(wheel, Sprites.Circle(), Palette.Gold, "rim"); Ui.Stretch(rim.rectTransform, -22, -22, -22, -22);
            var disc = Ui.Rect(wheel, "disc"); Ui.Stretch(disc);
            Color[] cols = { Palette.Hex("#4a8dff"), Palette.Hex("#a66cff"), Palette.Hex("#ff5a96"), Palette.Hex("#ff8a34"), Palette.Hex("#ffca2b"), Palette.Hex("#55dc57"), Palette.Hex("#19d4c1"), Palette.Hex("#ff5468") };
            int n = Economy.SpinPrizes.Length;
            for (int i = 0; i < n; i++)
            {
                var sec = Ui.Img(disc, Sprites.Circle(), cols[i % cols.Length], "sec" + i);
                Ui.Stretch(sec.rectTransform);
                sec.type = Image.Type.Filled; sec.fillMethod = Image.FillMethod.Radial360; sec.fillOrigin = (int)Image.Origin360.Top; sec.fillClockwise = true;
                sec.fillAmount = 1f / n - 0.004f;
                sec.rectTransform.localRotation = Quaternion.Euler(0, 0, -i * 360f / n);
                float ang = (i + 0.5f) * 360f / n;
                var lab = Ui.Rect(disc, "lab" + i);
                lab.sizeDelta = new Vector2(180, 90);
                float rad = ang * Mathf.Deg2Rad;
                lab.localPosition = new Vector3(Mathf.Sin(rad) * 235, Mathf.Cos(rad) * 235, 0);
                lab.localRotation = Quaternion.Euler(0, 0, -ang);
                var t = Ui.Label(lab, Economy.SpinPrizes[i].ToString(), 54, Color.white, TextAnchor.MiddleCenter);
                Ui.Stretch(t.rectTransform);
                var ci = Ui.Img(lab, Sprites.Coin(), Palette.Gold, "c"); Ui.At(ci.rectTransform, C, new Vector2(0, -52), new Vector2(46, 46));
            }
            var hub = Ui.Img(wheel, Sprites.Circle(), Palette.Hex("#2a1f5c"), "hub"); Ui.At(hub.rectTransform, C, Vector2.zero, new Vector2(130, 130));
            var hub2 = Ui.Img(hub.transform, Sprites.Circle(), Palette.Gold, "hub2"); Ui.At(hub2.rectTransform, C, Vector2.zero, new Vector2(96, 96));
            // pointer
            var ptr = Ui.Img(p.Card, Sprites.Heart(), Palette.Red, "ptr");
            ptr.rectTransform.sizeDelta = new Vector2(96, 96); ptr.rectTransform.localPosition = new Vector3(0, 90 + 395, 0);
            ptr.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);

            Button spinBtn = null; bool spinning = false;
            spinBtn = Ui.Btn(p.Card, Economy.SpinAvailable ? "SPIN!" : "SPIN AGAIN (VIDEO)", Palette.Green, Palette.GreenDark, new Vector2(700, 120), () =>
            {
                if (spinning) return;
                Action go = () =>
                {
                    spinning = true; spinBtn.interactable = false;
                    int k = UnityEngine.Random.Range(0, n);
                    float target = 360f * 5 + k * 360f / n + UnityEngine.Random.Range(-14f, 14f);
                    Sfx.Whoosh();
                    float last = 0;
                    Tween.Value(4.2f, v =>
                    {
                        if (disc == null) return;
                        float a = target * v;
                        disc.localRotation = Quaternion.Euler(0, 0, a);
                        if (Mathf.Floor(a / (360f / n)) != Mathf.Floor(last / (360f / n))) Sfx.Tick();
                        last = a;
                    }, Ease.OutCubic, () =>
                    {
                        int prize = Economy.SpinPrizes[k];
                        Save.Data.lastSpin = Save.Today; Economy.AddCoins(prize);
                        Sfx.Win(); Fx.I.Confetti(60);
                        App.I.Toast("You won " + prize + " coins!");
                        if (onDone != null) onDone();
                        Ui.Later(1.2f, () => { if (p != null) p.Close(); });
                    }, 0f, disc);
                };
                if (Economy.SpinAvailable) go(); else Monet.Rewarded("spin_again", go);
            }, 56).Pos(0, -480);
        }

        // ---------- quests ----------
        public static void QuestsPopup(Action onChanged)
        {
            Economy.EnsureQuestDay();
            var p = Popup.Create(new Vector2(960, 1260), "DAILY QUESTS", true, Palette.Hex("#19b8a6"));
            var refreshers = new System.Collections.Generic.List<Action>();
            Sprite[] icons = { Sprites.Check(), Sprites.Block(), Icons.Flame() };
            Color[] icolors = { Palette.Green, Palette.Block(1), Color.white };
            Action refreshAll = () => { for (int i = 0; i < refreshers.Count; i++) refreshers[i](); };

            for (int i = 0; i < 3; i++)
            {
                int q = i; var qd = Economy.Quests[q];
                float y = 340 - i * 235;
                var row = Ui.RoundImg(p.Card, new Color(0.05f, 0.03f, 0.2f, 0.5f), 40, "row"); row.rectTransform.sizeDelta = new Vector2(840, 205); row.Pos(0, y);
                var disc = Ui.Img(row.transform, Sprites.Circle(), Palette.Hex("#2b2370"), "disc"); Ui.At(disc.rectTransform, new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(120, 120));
                var ic = Ui.Img(disc.transform, icons[q], icolors[q], "ic"); Ui.At(ic.rectTransform, C, Vector2.zero, new Vector2(78, 78));
                var t = Ui.Label(row.transform, qd.Text, 46, Color.white, TextAnchor.MiddleLeft);
                Ui.At(t.rectTransform, new Vector2(0, 1), new Vector2(170, -52), new Vector2(430, 60)); t.rectTransform.pivot = new Vector2(0, 0.5f);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 26; t.resizeTextMaxSize = 46;
                RectTransform fill;
                var bar = Widgets.Bar(row.transform, new Vector2(430, 46), new Color(0, 0, 0, 0.4f), Palette.Green, out fill);
                Ui.At(bar.rectTransform, new Vector2(0, 0), new Vector2(385, 66), new Vector2(430, 46));
                var pt = Ui.Label(bar.transform, "", 30, Color.white, TextAnchor.MiddleCenter); Ui.Stretch(pt.rectTransform);
                var btn = Ui.Btn(row.transform, "", Palette.Gold, Palette.GoldDark, new Vector2(210, 116), null, 48);
                Ui.At((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(-135, 4), new Vector2(210, 116));
                var btnText = Ui.Label(btn.transform, "", 48, Color.white, TextAnchor.MiddleCenter); Ui.At(btnText.rectTransform, C, new Vector2(24, 0), new Vector2(150, 70));
                var coinIc = Ui.Img(btn.transform, Sprites.Coin(), Palette.Gold, "coin"); Ui.At(coinIc.rectTransform, C, new Vector2(-62, 0), new Vector2(56, 56));
                var checkIc = Ui.Img(btn.transform, Sprites.Check(), Color.white, "check"); Ui.At(checkIc.rectTransform, C, Vector2.zero, new Vector2(70, 70));
                var face = btn.transform.Find("face").GetComponent<Image>(); var lip = btn.transform.Find("lip").GetComponent<Image>();
                bool shined = false;
                float barW = 430;
                Action refresh = () =>
                {
                    int prog = Save.Data.questProgress[q];
                    bool done = prog >= qd.Target, claimed = Save.Data.questClaimed[q];
                    Widgets.SetBar(fill, barW, prog / (float)qd.Target);
                    pt.text = Mathf.Min(prog, qd.Target) + " / " + qd.Target;
                    checkIc.gameObject.SetActive(claimed); coinIc.gameObject.SetActive(!claimed); btnText.gameObject.SetActive(!claimed);
                    btnText.text = qd.Reward.ToString();
                    face.color = claimed ? Palette.Hex("#3b8f5b") : (done ? Palette.Gold : Palette.Hex("#6c6a9a"));
                    lip.color = claimed ? Palette.Hex("#1f6a3b") : (done ? Palette.GoldDark : Palette.Hex("#45447a"));
                    if (done && !claimed && !shined) { shined = true; Anim.AddShine(btn, 2.2f); Anim.Breathe(btn.transform.Find("face"), 0.04f, 1.1f); }
                };
                refreshers.Add(refresh);
                btn.onClick.AddListener(() =>
                {
                    bool done = Save.Data.questProgress[q] >= qd.Target;
                    if (!done) { Anim.Shake(btn.transform, 10f, 0.3f); Sfx.Invalid(); return; }
                    if (Save.Data.questClaimed[q]) return;
                    Save.Data.questClaimed[q] = true; Economy.AddCoins(qd.Reward);
                    Sfx.Reward();
                    if (Fx.I != null)
                    {
                        Fx.I.Burst(btn.transform.position, Palette.Gold, 22, 650f, 32f);
                        Fx.I.Float(btn.transform.position + new Vector3(0, 40, 0), "+" + qd.Reward, 78, Palette.Gold, 150f, 1f);
                    }
                    Anim.Flash(row, Palette.Alpha(Palette.Green, 0.8f), 0.5f);
                    Tween.Punch(btn.transform, 0.25f, 0.3f);
                    refreshAll();
                    if (onChanged != null) onChanged();
                });
                refresh();
            }

            // bonus for finishing everything
            var bonus = Ui.RoundImg(p.Card, new Color(1f, 0.78f, 0.2f, 0.18f), 40, "bonus"); bonus.rectTransform.sizeDelta = new Vector2(840, 150); bonus.Pos(0, -395);
            var gi = Ui.Img(bonus.transform, Icons.Gift(), Color.white, "gift"); Ui.At(gi.rectTransform, new Vector2(0, 0.5f), new Vector2(86, 0), new Vector2(110, 110));
            Anim.Wiggle(gi.transform, 8f, 1f);
            Ui.LabelAt(bonus.transform, "FINISH ALL 3", 42, Palette.Gold, new Vector2(0, 0.5f), new Vector2(330, 22), new Vector2(380, 56), TextAnchor.MiddleLeft);
            Ui.LabelAt(bonus.transform, "BONUS CHEST", 30, Color.white, new Vector2(0, 0.5f), new Vector2(330, -26), new Vector2(380, 40), TextAnchor.MiddleLeft, false);
            var bb = Ui.Btn(bonus.transform, "", Palette.Gold, Palette.GoldDark, new Vector2(230, 100), null, 44);
            Ui.At((RectTransform)bb.transform, new Vector2(1, 0.5f), new Vector2(-145, 4), new Vector2(230, 100));
            var bbText = Ui.Label(bb.transform, "", 46, Color.white, TextAnchor.MiddleCenter); Ui.Stretch(bbText.rectTransform);
            var bface = bb.transform.Find("face").GetComponent<Image>(); var blip = bb.transform.Find("lip").GetComponent<Image>();
            Action refreshBonus = () =>
            {
                bool all = Save.Data.questClaimed[0] && Save.Data.questClaimed[1] && Save.Data.questClaimed[2];
                bool got = Save.Data.questBonusClaimed;
                bbText.text = got ? "CLAIMED" : "+" + Economy.QuestBonusCoins;
                bbText.fontSize = got ? 34 : 46;
                bface.color = got ? Palette.Hex("#3b8f5b") : (all ? Palette.Gold : Palette.Hex("#6c6a9a"));
                blip.color = got ? Palette.Hex("#1f6a3b") : (all ? Palette.GoldDark : Palette.Hex("#45447a"));
            };
            refreshers.Add(refreshBonus);
            bb.onClick.AddListener(() =>
            {
                bool all = Save.Data.questClaimed[0] && Save.Data.questClaimed[1] && Save.Data.questClaimed[2];
                if (!all || Save.Data.questBonusClaimed) { Anim.Shake(bb.transform, 10f, 0.3f); Sfx.Invalid(); return; }
                Save.Data.questBonusClaimed = true; Economy.AddCoins(Economy.QuestBonusCoins); Economy.AddBooster(1, 1);
                Sfx.Win();
                if (Fx.I != null) { Fx.I.Confetti(70); Fx.I.Float(bb.transform.position, "+" + Economy.QuestBonusCoins + " & BOMB!", 70, Palette.Gold, 160f, 1.3f); }
                refreshAll();
                if (onChanged != null) onChanged();
            });
            refreshBonus();
            var info = Ui.Label(p.Card, "Quests reset every day", 30, Palette.Alpha(Color.white, 0.7f), TextAnchor.MiddleCenter, false); info.Pos(0, -520);
        }

        // ---------- themes ----------
        public static void Themes(Action onChanged)
        {
            var p = Popup.Create(new Vector2(960, 1260), "THEMES", true, Palette.Hex("#ff5a96"));
            var refreshers = new System.Collections.Generic.List<Action>();
            Action refreshAll = () => { for (int i = 0; i < refreshers.Count; i++) refreshers[i](); };
            for (int i = 0; i < Palette.Themes.Length; i++)
            {
                int k = i; var th = Palette.Themes[i];
                float x = (i % 2 == 0) ? -230 : 230, y = 260 - (i / 2) * 470;
                var card = Ui.Rect(p.Card, "theme" + i); card.sizeDelta = new Vector2(420, 430); card.localPosition = new Vector3(x, y, 0);
                var frame = Ui.Img(card, Sprites.Glossy(28), Palette.Hex("#5a47b8"), "frame"); Ui.Stretch(frame.rectTransform);
                var bgb = Ui.RoundImg(card, th.BgBottom, 24, "bgb"); Ui.Stretch(bgb.rectTransform, 12, 110, 12, 12);
                var bgt = Ui.Img(card, Sprites.Fade(), th.BgTop, "bgt"); Ui.Stretch(bgt.rectTransform, 12, 110, 12, 12);
                var brd = Ui.RoundImg(card, th.Board, 20, "brd"); Ui.At(brd.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(250, 200));
                for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
                {
                    bool fillCell = (r + c) % 2 == 0 || r == 1;
                    var ce = Ui.Img(brd.transform, fillCell ? Sprites.Block() : Sprites.Cell(), fillCell ? Palette.Block(r * 3 + c) : th.Cell, "c");
                    Ui.At(ce.rectTransform, C, new Vector2((c - 1) * 70, (1 - r) * 62), new Vector2(60, 60));
                }
                var nm = Ui.Label(card, th.Name.ToUpper(), 38, Color.white, TextAnchor.MiddleCenter); Ui.At(nm.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(380, 50));
                var b = Ui.Btn(card, "", Palette.Green, Palette.GreenDark, new Vector2(300, 72), null, 40);
                Ui.At((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(300, 72));
                var bt = Ui.Label(b.transform, "", 40, Color.white, TextAnchor.MiddleCenter); Ui.At(bt.rectTransform, C, new Vector2(20, 0), new Vector2(200, 60));
                var bc = Ui.Img(b.transform, Sprites.Coin(), Palette.Gold, "coin"); Ui.At(bc.rectTransform, C, new Vector2(-60, 0), new Vector2(44, 44));
                var bface = b.transform.Find("face").GetComponent<Image>(); var blip = b.transform.Find("lip").GetComponent<Image>();
                Action refresh = () =>
                {
                    bool owned = Save.Data.themeOwned[k], use = Save.Data.theme == k;
                    frame.color = use ? Palette.Gold : Palette.Hex("#5a47b8");
                    bt.text = use ? "IN USE" : (owned ? "USE" : th.Price.ToString());
                    bt.rectTransform.anchoredPosition = new Vector2(owned ? 0 : 20, 0);
                    bc.gameObject.SetActive(!owned);
                    bface.color = use ? Palette.Hex("#7a76a8") : (owned ? Palette.Green : Palette.Gold);
                    blip.color = use ? Palette.Hex("#4c4880") : (owned ? Palette.GreenDark : Palette.GoldDark);
                };
                refreshers.Add(refresh);
                b.onClick.AddListener(() =>
                {
                    if (!Save.Data.themeOwned[k])
                    {
                        if (!Economy.Spend(th.Price)) { Anim.Shake(b.transform, 10f, 0.3f); Sfx.Invalid(); App.I.Toast("Not enough coins"); return; }
                        Save.Data.themeOwned[k] = true;
                        Sfx.Reward(); if (Fx.I != null) Fx.I.Burst(b.transform.position, Palette.Gold, 20, 600f, 30f);
                    }
                    Save.Data.theme = k; Save.Commit(); App.I.ApplyTheme(); Tween.Punch(card, 0.08f, 0.3f);
                    refreshAll();
                    if (onChanged != null) onChanged();
                });
                refresh();
            }
        }

        // ---------- hearts ----------
        public static void NoHearts(Action onRefilled)
        {
            var p = Popup.Create(new Vector2(900, 1100), "OUT OF HEARTS", true, Palette.Red);
            var h = Ui.Img(p.Card, Sprites.Heart(), Palette.Red, "h"); h.rectTransform.sizeDelta = new Vector2(240, 240); h.Pos(0, 240);
            Tween.Punch(h.transform, 0.1f, 0.8f);
            var t = Ui.Label(p.Card, Save.Data.hearts >= Economy.MaxHearts ? "Hearts are full!" : "Next heart in " + Ui.Clock(Economy.UntilNextHeart()), 44, Color.white, TextAnchor.MiddleCenter);
            t.Pos(0, 70);
            Ui.Btn(p.Card, "REFILL  (VIDEO)", Palette.Green, Palette.GreenDark, new Vector2(700, 120), () =>
            {
                Monet.Rewarded("heart_refill", () => { Economy.AddHearts(Economy.MaxHearts); p.Close(); if (onRefilled != null) onRefilled(); });
            }, 52).Pos(0, -90);
            Ui.Btn(p.Card, "REFILL  " + Economy.HeartRefillCost + " COINS", Palette.Gold, Palette.GoldDark, new Vector2(700, 120), () =>
            {
                if (!Economy.Spend(Economy.HeartRefillCost)) { p.Close(); Shop(); return; }
                Economy.AddHearts(Economy.MaxHearts); p.Close(); if (onRefilled != null) onRefilled();
            }, 48).Pos(0, -250);
            Ui.Btn(p.Card, "PLAY CLASSIC  (NO HEARTS)", Palette.Blue, Palette.BlueDark, new Vector2(700, 100), () => { p.Close(true); App.I.StartClassic(); }, 38).Pos(0, -385);
            Ui.Label(p.Card, "Hearts come back every " + Economy.HeartRegenMinutes + " minutes", 30, Palette.Alpha(Color.white, 0.7f), TextAnchor.MiddleCenter, false).Pos(0, -470);
        }

        // ---------- booster offer ----------
        public static void BoosterOffer(int kind, Action onGranted)
        {
            string nm = Economy.BoosterName(kind);
            var p = Popup.Create(new Vector2(880, 760), "GET " + nm, true, Palette.Blue);
            Ui.Label(p.Card, "You are out of " + nm.ToLower() + "s", 44, Color.white, TextAnchor.MiddleCenter).Pos(0, 170);
            Ui.Btn(p.Card, "+1 FREE  (VIDEO)", Palette.Green, Palette.GreenDark, new Vector2(680, 120), () =>
            {
                Monet.Rewarded("booster_" + nm.ToLower(), () => { Economy.AddBooster(kind, 1); p.Close(); if (onGranted != null) onGranted(); });
            }, 50).Pos(0, 20);
            Ui.Btn(p.Card, "+1 FOR " + Economy.BoosterPrice(kind) + " COINS", Palette.Gold, Palette.GoldDark, new Vector2(680, 120), () =>
            {
                if (!Economy.Spend(Economy.BoosterPrice(kind))) { p.Close(); Shop(); return; }
                Economy.AddBooster(kind, 1); p.Close(); if (onGranted != null) onGranted();
            }, 46).Pos(0, -140);
        }
    }
}
