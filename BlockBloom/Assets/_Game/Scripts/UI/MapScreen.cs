using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;
using BlockBloom.Logic;

namespace BlockBloom
{
    /// <summary>Level preview pop-up shown before every adventure level.</summary>
    public static class LevelUi
    {
        public static string GoalIcon(Goal g) { return Adventure.ShortGoal(g); }

        public static Sprite GoalSprite(Goal g)
        {
            switch (g.Type)
            {
                case GoalType.Gems: return Sprites.Diamond();
                case GoalType.Lines: return Sprites.Check();
                case GoalType.Combo: return Sprites.Star();
                case GoalType.Score: return Sprites.Star();
                default: return Sprites.Block();
            }
        }

        public static Color GoalColour(Goal g)
        {
            switch (g.Type)
            {
                case GoalType.Gems: return Palette.Hex("#6fe3ff");
                case GoalType.Lines: return Palette.Green;
                case GoalType.Combo: return Palette.Hex("#ff8a34");
                case GoalType.Score: return Palette.Gold;
                default: return Palette.Block(g.Colour);
            }
        }

        public static void Open(int level)
        {
            if (level > Save.Data.unlockedLevel) { App.I.Toast("Finish the previous levels first"); return; }
            var d = Adventure.Get(level);
            var p = Popup.Create(new Vector2(900, 1000), "LEVEL " + level, true, d.IsBoss ? Palette.Hex("#ff4d6d") : Palette.Gold);
            if (d.IsBoss) Ui.Label(p.Card, "BOSS LEVEL", 40, Palette.Hex("#ffd0d8"), TextAnchor.MiddleCenter).Pos(0, 330);
            Ui.Label(p.Card, "GOALS", 44, Palette.Alpha(Color.white, 0.85f), TextAnchor.MiddleCenter).Pos(0, 270);
            for (int i = 0; i < d.Goals.Length; i++)
            {
                var g = d.Goals[i];
                float y = 170 - i * 130;
                var row = Ui.RoundImg(p.Card, new Color(0.05f, 0.03f, 0.2f, 0.5f), 36, "row"); row.rectTransform.sizeDelta = new Vector2(700, 112); row.Pos(0, y);
                var ic = Ui.Img(row.transform, GoalSprite(g), GoalColour(g), "ic"); Ui.At(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(80, 80));
                var t = Ui.Label(row.transform, Adventure.GoalText(g), 44, Color.white, TextAnchor.MiddleLeft);
                Ui.At(t.rectTransform, new Vector2(0, 0.5f), new Vector2(420, 0), new Vector2(560, 80));
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 24; t.resizeTextMaxSize = 44;
            }
            var mv = Ui.Label(p.Card, "IN " + d.MaxMoves + " MOVES", 54, Palette.Gold, TextAnchor.MiddleCenter);
            mv.Pos(0, 170 - d.Goals.Length * 130 - 10);
            Widgets.Stars(p.Card, Save.Data.stars[level - 1], 90, 20).Pos(0, -190);
            Ui.Btn(p.Card, "PLAY", Palette.Green, Palette.GreenDark, new Vector2(620, 140), () =>
            {
                Economy.TickHearts();
                if (Save.Data.hearts <= 0) { p.Close(); Popups.NoHearts(() => App.I.StartLevel(level)); return; }
                p.Close(true); App.I.StartLevel(level);
            }, 78).Pos(0, -350);
        }
    }

    /// <summary>Scrolling adventure map with a winding path. Nodes are pooled so 300 levels cost nothing.</summary>
    public sealed class MapScreen : ScreenBase
    {
        private const float Spacing = 190f;
        private const int PoolSize = 18;

        private RectTransform _content, _view;
        private ScrollRect _scroll;
        private readonly List<Node> _pool = new List<Node>();
        private float _lastY = -99999f;
        private Text _chapter, _chapterSub;
        private RectTransform _locate; private Text _locateText; private RectTransform _locateArrow;
        private static readonly string[] ChapterNames =
        {
            "Sunny Meadow", "Candy Hills", "Crystal Caves", "Neon City", "Star Harbor", "Frozen Peaks", "Lava Lake", "Cloud Kingdom",
            "Toy Workshop", "Midnight Forest", "Golden Desert", "Jelly Reef", "Galaxy Gate", "Rainbow Bridge", "Dragon Valley"
        };

        private sealed class Node
        {
            public RectTransform Rt; public Image Face, Lip; public Text Label; public RectTransform Stars; public Image Lock; public int Level = -1;
            public List<Image> Dots = new List<Image>(); public Image Ring; public RectTransform Marker;
        }

        public void Build()
        {
            // viewport
            _view = Ui.Rect(Rt, "view");
            Ui.Stretch(_view, 0, 200, 0, 0);
            _view.gameObject.AddComponent<RectMask2D>();
            _content = Ui.Rect(_view, "content");
            _content.anchorMin = new Vector2(0, 0); _content.anchorMax = new Vector2(1, 0); _content.pivot = new Vector2(0.5f, 0);
            float h = Spacing * (Adventure.LevelCount + 2);
            _content.sizeDelta = new Vector2(0, h); _content.anchoredPosition = Vector2.zero;
            var bgHit = Ui.Img(_content, Sprites.Square(), new Color(1, 1, 1, 0.001f), "hit", true);
            Ui.Stretch(bgHit.rectTransform);
            _scroll = _view.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content; _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic; _scroll.inertia = true; _scroll.decelerationRate = 0.12f;
            _scroll.scrollSensitivity = 40f;

            for (int i = 0; i < PoolSize; i++) _pool.Add(MakeNode());

            // clear navigation: top bar, chapter banner, bottom nav with HOME / PLAY / SHOP
            var chapterBar = Ui.Rect(Rt, "chapterBar");
            chapterBar.sizeDelta = new Vector2(720, 112); chapterBar.PosA(0.5f, 1f, 0, -215);
            var cbg = Ui.RoundImg(chapterBar, new Color(0.06f, 0.03f, 0.22f, 0.82f), 48, "bg"); Ui.Stretch(cbg.rectTransform);
            _chapter = Ui.LabelAt(chapterBar, "CHAPTER 1", 44, Palette.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(680, 54));
            _chapterSub = Ui.LabelAt(chapterBar, "Sunny Meadow", 32, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(680, 40), TextAnchor.MiddleCenter, false);

            TopBar.Create(Rt, false);

            var nav = Ui.Rect(Rt, "nav");
            nav.anchorMin = new Vector2(0, 0); nav.anchorMax = new Vector2(1, 0); nav.pivot = new Vector2(0.5f, 0);
            nav.sizeDelta = new Vector2(0, 200); nav.anchoredPosition = Vector2.zero;
            var navBg = Ui.Img(nav, Sprites.Square(), new Color(0.07f, 0.04f, 0.26f, 0.96f), "bg"); Ui.Stretch(navBg.rectTransform);
            var navTop = Ui.Img(nav, Sprites.Square(), new Color(1, 1, 1, 0.12f), "edge"); navTop.rectTransform.anchorMin = new Vector2(0, 1); navTop.rectTransform.anchorMax = new Vector2(1, 1);
            navTop.rectTransform.pivot = new Vector2(0.5f, 1); navTop.rectTransform.sizeDelta = new Vector2(0, 4); navTop.rectTransform.anchoredPosition = Vector2.zero;

            NavButton(nav, "HOME", Icons.HomeColour(), Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), -360, () => App.I.ShowHome());
            NavButton(nav, "SHOP", Icons.Bag(), Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), 360, Popups.Shop);
            int curLv = Mathf.Clamp(Save.Data.unlockedLevel, 1, Adventure.LevelCount);
            var play = Ui.Btn(nav, "", Palette.Green, Palette.GreenDark, new Vector2(470, 150), () => LevelUi.Open(curLv), 60, "play");
            Ui.At((RectTransform)play.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(470, 150));
            Ui.LabelAt(play.transform, "PLAY", 66, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(430, 76));
            Ui.LabelAt(play.transform, "LEVEL " + curLv, 36, Palette.Hex("#d9ffe3"), new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(430, 44), TextAnchor.MiddleCenter, false);
            Anim.AddShine(play, 2.8f);
            Anim.Breathe(play.transform.Find("face"), 0.03f, 1.4f);

            // jump-to-current pill
            _locate = Ui.Rect(Rt, "locate");
            _locate.sizeDelta = new Vector2(300, 90); _locate.PosA(0.5f, 0f, 0, 305);
            var lbg = Ui.RoundImg(_locate, Palette.Hex("#ff8a34"), 45, "bg", true); Ui.Stretch(lbg.rectTransform);
            var lbtn = _locate.gameObject.AddComponent<Button>(); lbtn.targetGraphic = lbg; lbtn.transition = Selectable.Transition.None;
            lbtn.onClick.AddListener(() => { Sfx.Click(); ScrollToCurrent(true); });
            _locate.gameObject.AddComponent<PressFx>();
            _locateText = Ui.LabelAt(_locate, "YOU ARE HERE", 34, Color.white, new Vector2(0.5f, 0.5f), new Vector2(24, 0), new Vector2(240, 60));
            var arrow = Ui.Img(_locate, Sprites.Play(), Color.white, "arrow"); Ui.At(arrow.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(46, 46));
            _locateArrow = arrow.rectTransform;
            _locate.gameObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
            ScrollToCurrent(false);
            Refresh();
        }

        private void NavButton(Transform nav, string label, Sprite icon, Color face, Color lip, float x, Action a)
        {
            var b = Ui.Btn(nav, "", face, lip, new Vector2(210, 150), a, 36, label.ToLower());
            Ui.At((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(x, 4), new Vector2(210, 150));
            var ic = Ui.Img(b.transform, icon, Color.white, "ic"); Ui.At(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(84, 84));
            Ui.LabelAt(b.transform, label, 34, Color.white, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(200, 44));
        }

        private void ScrollToCurrent(bool animate)
        {
            float viewH = _view.rect.height;
            float h = _content.sizeDelta.y;
            int cur = Mathf.Clamp(Save.Data.unlockedLevel, 1, Adventure.LevelCount);
            float target = Mathf.Clamp(viewH * 0.42f - YOf(cur), viewH - h, 0);
            if (_scroll != null) _scroll.StopMovement();
            if (!animate) { _content.anchoredPosition = new Vector2(0, target); return; }
            float from = _content.anchoredPosition.y;
            Tween.Value(0.6f, k => { if (_content != null) _content.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(from, target, k)); }, Ease.OutCubic, null, 0f, _content);
        }

        private static float YOf(int level) { return 240f + (level - 1) * Spacing; }
        private static float XOf(int level) { return Mathf.Sin(level * 0.62f) * 300f; }

        private Node MakeNode()
        {
            var n = new Node();
            n.Rt = Ui.Rect(_content, "node");
            n.Rt.anchorMin = n.Rt.anchorMax = new Vector2(0.5f, 0f);
            n.Rt.sizeDelta = new Vector2(150, 150);
            for (int i = 0; i < 3; i++)
            {
                var d = Ui.Img(n.Rt, Sprites.Circle(), new Color(1, 1, 1, 0.45f), "dot");
                d.rectTransform.sizeDelta = new Vector2(20, 20);
                n.Dots.Add(d);
            }
            n.Ring = Ui.Img(n.Rt, Sprites.Glow(), Palette.Alpha(Color.white, 0.8f), "ring"); Ui.At(n.Ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
            n.Lip = Ui.Img(n.Rt, Sprites.Circle(), Palette.GoldDark, "lip"); Ui.At(n.Lip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(140, 140));
            n.Face = Ui.Img(n.Rt, Sprites.Circle(), Palette.Gold, "face", true); Ui.At(n.Face.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140));
            n.Label = Ui.Label(n.Rt, "1", 64, Color.white, TextAnchor.MiddleCenter); Ui.At(n.Label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(140, 90));
            n.Lock = Ui.Img(n.Rt, Sprites.Lock(), Palette.Alpha(Color.white, 0.8f), "lock"); Ui.At(n.Lock.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            var sr = Ui.Rect(n.Rt, "stars"); sr.sizeDelta = new Vector2(150, 50); sr.anchoredPosition = new Vector2(0, -92); n.Stars = sr;
            for (int i = 0; i < 3; i++)
            {
                var s = Ui.Img(sr, Sprites.Star(), Palette.Gold, "s" + i);
                Ui.At(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 46, i == 1 ? 8 : 0), new Vector2(i == 1 ? 56 : 46, i == 1 ? 56 : 46));
            }
            var mk = Ui.Rect(n.Rt, "marker"); mk.sizeDelta = new Vector2(150, 110); mk.anchoredPosition = new Vector2(0, 118);
            var mkBg = Ui.RoundImg(mk, Palette.Hex("#ff4d6d"), 34, "bg"); mkBg.rectTransform.sizeDelta = new Vector2(120, 62); mkBg.rectTransform.anchoredPosition = new Vector2(0, 14);
            var mkTip = Ui.Img(mk, Sprites.Play(), Palette.Hex("#ff4d6d"), "tip"); mkTip.rectTransform.sizeDelta = new Vector2(34, 34); mkTip.rectTransform.anchoredPosition = new Vector2(0, -26); mkTip.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            Ui.LabelAt(mk, "YOU", 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(120, 50));
            n.Marker = mk; mk.gameObject.AddComponent<BobFx>().Amp = 9f;
            var mkBob = mk.GetComponent<BobFx>(); mkBob.Speed = 4f; mkBob.Base = mk.anchoredPosition;
            var b = n.Rt.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = n.Face;
            n.Rt.gameObject.AddComponent<PressFx>();
            b.onClick.AddListener(() => { if (n.Level > 0) { Sfx.Click(); LevelUi.Open(n.Level); } });
            return n;
        }

        private void Refresh()
        {
            float viewH = _view.rect.height;
            float scrolled = -_content.anchoredPosition.y;               // distance of the viewport bottom above content bottom... (>=0)
            float bottomY = scrolled - 250f, topY = scrolled + viewH + 250f;
            int first = Mathf.Max(1, Mathf.FloorToInt((bottomY - 240f) / Spacing) + 1);
            int last = Mathf.Min(Adventure.LevelCount, Mathf.CeilToInt((topY - 240f) / Spacing) + 1);
            int cur = Save.Data.unlockedLevel;
            int count = last - first + 1;
            if (count > PoolSize) { last = first + PoolSize - 1; }
            int pi = 0;
            for (int lv = first; lv <= last && pi < _pool.Count; lv++, pi++)
            {
                var n = _pool[pi];
                n.Rt.gameObject.SetActive(true);
                n.Level = lv;
                n.Rt.anchoredPosition = new Vector2(XOf(lv), YOf(lv));
                bool done = lv < cur || (lv == cur && Save.Data.stars[lv - 1] > 0);
                bool current = lv == cur;
                bool locked = lv > cur;
                bool boss = lv % 10 == 0;
                Color face = locked ? Palette.Hex("#7d79ad") : (current ? Palette.Green : (boss ? Palette.Hex("#ff4d6d") : Palette.Gold));
                Color lip = locked ? Palette.Hex("#514d82") : (current ? Palette.GreenDark : (boss ? Palette.Hex("#b0233f") : Palette.GoldDark));
                n.Face.color = face; n.Lip.color = lip;
                float sz = boss ? 168f : 140f;
                n.Face.rectTransform.sizeDelta = new Vector2(sz, sz); n.Lip.rectTransform.sizeDelta = new Vector2(sz, sz);
                n.Label.text = locked ? "" : lv.ToString();
                n.Lock.gameObject.SetActive(locked);
                n.Ring.gameObject.SetActive(current);
                n.Marker.gameObject.SetActive(current);
                n.Stars.gameObject.SetActive(!locked && Save.Data.stars[lv - 1] > 0);
                if (n.Stars.gameObject.activeSelf)
                    for (int i = 0; i < 3; i++) n.Stars.GetChild(i).GetComponent<Image>().color = i < Save.Data.stars[lv - 1] ? Palette.Gold : new Color(0.1f, 0.06f, 0.3f, 0.6f);
                // dots to the next level
                bool showDots = lv < Adventure.LevelCount;
                for (int i = 0; i < n.Dots.Count; i++)
                {
                    n.Dots[i].gameObject.SetActive(showDots);
                    if (!showDots) continue;
                    float k = (i + 1) / 4f;
                    Vector2 a = new Vector2(XOf(lv), YOf(lv)), b = new Vector2(XOf(lv + 1), YOf(lv + 1));
                    n.Dots[i].rectTransform.anchoredPosition = Vector2.Lerp(a, b, k) - a;
                    n.Dots[i].color = new Color(1, 1, 1, lv < cur ? 0.7f : 0.3f);
                }
            }
            for (; pi < _pool.Count; pi++) { _pool[pi].Level = -1; _pool[pi].Rt.gameObject.SetActive(false); }
        }

        private void Update()
        {
            if (_content == null) return;
            float y = _content.anchoredPosition.y;
            if (Mathf.Abs(y - _lastY) > 8f) { _lastY = y; Refresh(); }
            UpdateChapterAndLocate();
            // pulse the current node ring
            int cur = Save.Data.unlockedLevel;
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i].Level == cur)
                {
                    float s = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 3f);
                    _pool[i].Rt.localScale = Vector3.one * s;
                }
                else if (_pool[i].Level > 0) _pool[i].Rt.localScale = Vector3.one;
        }

        private void UpdateChapterAndLocate()
        {
            float viewH = _view.rect.height;
            float mid = -_content.anchoredPosition.y + viewH * 0.5f;
            int lv = Mathf.Clamp(Mathf.RoundToInt((mid - 240f) / Spacing) + 1, 1, Adventure.LevelCount);
            int ch = (lv - 1) / 10;
            _chapter.text = "CHAPTER " + (ch + 1) + "   •   LEVELS " + (ch * 10 + 1) + "-" + (ch * 10 + 10);
            _chapterSub.text = ChapterNames[ch % ChapterNames.Length];
            int cur = Mathf.Clamp(Save.Data.unlockedLevel, 1, Adventure.LevelCount);
            float curY = YOf(cur) + _content.anchoredPosition.y;     // in viewport space
            bool visible = curY > 120f && curY < viewH - 120f;
            if (_locate.gameObject.activeSelf == visible)
            {
                _locate.gameObject.SetActive(!visible);
                if (!visible) Anim.PopIn(_locate, 0f, 0.5f, 0.3f);
            }
            if (!visible)
            {
                bool above = curY >= viewH - 120f;
                _locateText.text = above ? "GO TO LEVEL " + cur : "BACK TO LEVEL " + cur;
                _locateArrow.localRotation = Quaternion.Euler(0, 0, above ? 90 : -90);
                _locate.sizeDelta = new Vector2(420, 90);
                _locateText.rectTransform.sizeDelta = new Vector2(340, 60);
            }
        }

        public override void OnBack() { App.I.ShowHome(); }
    }
}
