using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;
using BlockBloom.Logic;

namespace BlockBloom
{
    /// <summary>The gameplay screen: HUD, board, tray, boosters, and the win / lose flow.</summary>
    public sealed class GameScreen : ScreenBase
    {
        private static readonly Dictionary<int, int> Attempts = new Dictionary<int, int>();

        public GameSession S;
        private Mode _mode;
        private int _levelIndex;
        private LevelDef _def;
        private BoardView _board;
        private TrayView _tray;
        private Text _score, _moves, _best, _titleT;
        private Text[] _goalText = new Text[2];
        private Image[] _goalIcon = new Image[2];
        private RectTransform[] _goalFill = new RectTransform[2];
        private float[] _goalFillW = new float[2];
        private readonly List<Image> _stars = new List<Image>();
        private readonly Text[] _boosterCount = new Text[3];
        private readonly Image[] _boosterBadge = new Image[3];
        private GameObject _bombOverlay;
        private bool _bombMode, _ended, _almostShown;
        private float _shownScore;
        private int _tutorialStage;
        private RectTransform _finger; private Text _tutorialText;
        private int _placements;
        private float _startTime;

        // ---------- setup ----------
        public void Begin(Mode mode, int level)
        {
            _mode = mode; _levelIndex = level;
            int seed;
            if (mode == Mode.Adventure)
            {
                _def = Adventure.Get(level);
                int a; Attempts.TryGetValue(level, out a); Attempts[level] = a + 1;
                seed = _def.Seed + a * 7;
            }
            else if (mode == Mode.Daily) seed = Economy.DailySeed();
            else seed = Environment.TickCount;
            S = new GameSession(mode, seed, _def);
            _startTime = Time.realtimeSinceStartup;
            BuildUi();
            _board.Sync(S.Board);
            _tray.Show(S, true);
            UpdateHud(true);
            Monet.Log("level_start", "mode", mode.ToString(), "level", level);
            if (mode == Mode.Adventure && !Save.Data.tutorialDone && level == 1) StartTutorial();
            if (mode == Mode.Adventure) Economy.QuestAdd(1, 0);
        }

        private void BuildUi()
        {
            // top row
            var back = Ui.IconBtn(Rt, Sprites.Home(), Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), 100, () => OnBack());
            back.PosA(0f, 1f, 90, -80);
            var pause = Ui.IconBtn(Rt, Sprites.Pause(), Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), 100, () => PausePopup());
            pause.PosA(1f, 1f, -90, -80);
            string title = _mode == Mode.Adventure ? "LEVEL " + _levelIndex : (_mode == Mode.Classic ? "CLASSIC" : "DAILY CHALLENGE");
            _titleT = Ui.LabelAt(Rt, title, 64, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(560, 100));

            // HUD card
            var card = Ui.Card(Rt, new Vector2(1000, 230), Palette.Alpha(Palette.Themes[Save.Data.theme].PanelDark, 0.92f), 40, "hud");
            card.PosA(0.5f, 1f, 0, -290);

            if (_mode == Mode.Adventure)
            {
                var mv = Ui.RoundImg(card, new Color(0, 0, 0, 0.28f), 34, "movesbox"); mv.rectTransform.sizeDelta = new Vector2(210, 180); mv.PosA(0f, 0.5f, 120, 0);
                Ui.LabelAt(mv.transform, "MOVES", 30, Palette.Hex("#bdb6ff"), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(200, 40), TextAnchor.MiddleCenter, false);
                _moves = Ui.LabelAt(mv.transform, "0", 100, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(200, 120));
                for (int i = 0; i < _def.Goals.Length; i++)
                {
                    var g = _def.Goals[i];
                    float y = _def.Goals.Length == 1 ? 10 : (i == 0 ? 50 : -42);
                    var ic = Ui.Img(card, LevelUi.GoalSprite(g), LevelUi.GoalColour(g), "gi" + i);
                    ic.rectTransform.sizeDelta = new Vector2(66, 66); ic.PosA(0f, 0.5f, 290, y);
                    _goalIcon[i] = ic;
                    RectTransform fill;
                    var bar = Widgets.Bar(card, new Vector2(330, 44), new Color(0, 0, 0, 0.4f), LevelUi.GoalColour(g), out fill);
                    bar.PosA(0f, 0.5f, 520, y - 12);
                    _goalFill[i] = fill; _goalFillW[i] = 330;
                    _goalText[i] = Ui.LabelAt(card, "", 34, Color.white, new Vector2(0f, 0.5f), new Vector2(520, y + 26), new Vector2(330, 40), TextAnchor.MiddleCenter, true);
                }
                Ui.LabelAt(card, "SCORE", 28, Palette.Hex("#bdb6ff"), new Vector2(1, 1), new Vector2(-105, -38), new Vector2(190, 36), TextAnchor.MiddleCenter, false);
                _score = Ui.LabelAt(card, "0", 56, Palette.Gold, new Vector2(1, 0.5f), new Vector2(-105, -20), new Vector2(190, 80));
                _score.resizeTextForBestFit = true; _score.resizeTextMinSize = 28; _score.resizeTextMaxSize = 56;
                // projected stars
                var sr = Ui.Rect(Rt, "stars"); sr.sizeDelta = new Vector2(300, 64); sr.PosA(0.5f, 1f, 0, -455);
                for (int i = 0; i < 3; i++)
                {
                    var s = Ui.Img(sr, Sprites.Star(), Palette.Gold, "s" + i);
                    Ui.At(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 88, i == 1 ? 8 : 0), new Vector2(i == 1 ? 76 : 64, i == 1 ? 76 : 64));
                    _stars.Add(s);
                }
            }
            else
            {
                Ui.LabelAt(card, "SCORE", 34, Palette.Hex("#bdb6ff"), new Vector2(0.5f, 1), new Vector2(-150, -42), new Vector2(300, 40), TextAnchor.MiddleCenter, false);
                _score = Ui.LabelAt(card, "0", 130, Palette.Gold, new Vector2(0.5f, 0.5f), new Vector2(-150, -18), new Vector2(480, 150));
                _score.resizeTextForBestFit = true; _score.resizeTextMinSize = 50; _score.resizeTextMaxSize = 130;
                var crown = Ui.Img(card, Sprites.Star(), Palette.Gold, "crown"); crown.rectTransform.sizeDelta = new Vector2(60, 60); crown.PosA(1f, 1f, -270, -62);
                Ui.LabelAt(card, "BEST", 28, Palette.Hex("#bdb6ff"), new Vector2(1, 1), new Vector2(-125, -50), new Vector2(150, 36), TextAnchor.MiddleLeft, false);
                int best = _mode == Mode.Classic ? Save.Data.classicBest : Save.Data.dailyBest;
                _best = Ui.LabelAt(card, Ui.Num(best), 60, Color.white, new Vector2(1, 0.5f), new Vector2(-165, -22), new Vector2(280, 80));
            }

            _board = BoardView.Create(Rt);
            _board.Rt.anchorMin = _board.Rt.anchorMax = new Vector2(0.5f, 0.5f);
            _board.Rt.anchoredPosition = new Vector2(0, _mode == Mode.Adventure ? -20 : 0);
            _tray = TrayView.Create(Rt, _board, App.I.DragLayer);
            _tray.PosA(0.5f, 0.5f, 0, -635);
            _tray.Dropped = OnDropped;

            // boosters
            string[] names = { "UNDO", "BOMB", "SHUFFLE" };
            Sprite[] icons = { Sprites.Undo(), Sprites.Bomb(), Sprites.Shuffle() };
            Color[] faces = { Palette.Blue, Palette.Hex("#ff6a3d"), Palette.Hex("#19b8a6") };
            Color[] lips = { Palette.BlueDark, Palette.Hex("#b8381a"), Palette.Hex("#0f7f72") };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = Ui.Btn(Rt, "", faces[i], lips[i], new Vector2(190, 150), () => UseBooster(k), 36, "booster" + i);
                b.PosA(0.5f, 0f, (i - 1) * 270, 130);
                var ic = Ui.Img(b.transform, icons[i], Color.white, "ic"); Ui.At(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(76, 76));
                Ui.LabelAt(b.transform, names[i], 28, Color.white, new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(180, 36));
                var badge = Ui.Img(b.transform, Sprites.Circle(), Palette.Red, "badge"); Ui.At(badge.rectTransform, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(60, 60));
                _boosterBadge[i] = badge;
                _boosterCount[i] = Ui.Label(badge.transform, "0", 38, Color.white, TextAnchor.MiddleCenter, false);
                Ui.Stretch(_boosterCount[i].rectTransform);
            }
            RefreshBoosters();

            // bomb targeting overlay (over the board only)
            var ov = Ui.Img(_board.Rt, Sprites.Square(), new Color(0, 0, 0, 0.35f), "bombOverlay", true);
            Ui.Stretch(ov.rectTransform);
            var ob = ov.gameObject.AddComponent<Button>(); ob.transition = Selectable.Transition.None;
            ov.gameObject.AddComponent<BombTap>().Owner = this;
            var msg = Ui.Label(ov.transform, "TAP A BLOCK TO BLAST 3x3", 48, Color.white, TextAnchor.MiddleCenter);
            Ui.At(msg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 40), new Vector2(900, 70));
            _bombOverlay = ov.gameObject; _bombOverlay.SetActive(false);
        }

        public sealed class BombTap : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
        {
            public GameScreen Owner;
            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { Owner.BombAt(e.position); }
        }

        // ---------- HUD ----------
        private void UpdateHud(bool instant)
        {
            if (_mode == Mode.Adventure)
            {
                _moves.text = Mathf.Max(0, S.MovesLeft).ToString();
                _moves.color = S.MovesLeft <= 3 ? Palette.Hex("#ff6b6b") : Color.white;
                for (int i = 0; i < _def.Goals.Length; i++)
                {
                    var g = _def.Goals[i];
                    int p = Mathf.Min(S.GoalProgress[i], g.Target);
                    _goalText[i].text = Adventure.ShortGoal(g) + "  " + p + "/" + g.Target;
                    Widgets.SetBar(_goalFill[i], _goalFillW[i], S.GoalFraction(i));
                }
                int st = S.StarsNow();
                for (int i = 0; i < _stars.Count; i++) _stars[i].color = i < st ? Palette.Gold : new Color(0.1f, 0.06f, 0.3f, 0.55f);
            }
            if (instant) { _shownScore = S.Score; _score.text = Ui.Num(S.Score); }
        }

        private void RefreshBoosters()
        {
            for (int i = 0; i < 3; i++)
            {
                int n = Economy.BoosterCount(i);
                _boosterCount[i].text = n > 0 ? n.ToString() : "+";
                _boosterBadge[i].color = n > 0 ? Palette.Red : Palette.Green;
            }
        }

        private void Update()
        {
            if (S == null || _score == null) return;
            if (Mathf.Abs(_shownScore - S.Score) > 0.5f)
            {
                _shownScore = Mathf.MoveTowards(_shownScore, S.Score, Mathf.Max(1f, (S.Score - _shownScore) * 8f * Time.unscaledDeltaTime + 1f));
                _score.text = Ui.Num(Mathf.RoundToInt(_shownScore));
            }
            if (_mode != Mode.Adventure && _best != null)
            {
                int best = _mode == Mode.Classic ? Save.Data.classicBest : Save.Data.dailyBest;
                if (S.Score > best) _best.text = Ui.Num(S.Score);
            }
        }

        // ---------- tutorial ----------
        private void StartTutorial()
        {
            _tutorialStage = 1;
            _tutorialText = Ui.Label(Rt, "DRAG A BLOCK ONTO THE GRID", 54, Color.white, TextAnchor.MiddleCenter);
            _tutorialText.PosA(0.5f, 0.5f, 0, 520).Size(960, 120);
            _tutorialText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var f = Ui.Img(App.I.DragLayer, Sprites.Circle(), new Color(1, 1, 1, 0.85f), "finger");
            f.rectTransform.sizeDelta = new Vector2(96, 96);
            _finger = f.rectTransform;
            AnimateFinger();
        }

        private void AnimateFinger()
        {
            if (_finger == null || _tutorialStage != 1 || S == null) return;
            int r, c;
            int slot = 0;
            for (int i = 0; i < 3; i++) if (!S.Used[i]) { slot = i; break; }
            if (!Solver.BestPlacement(S.Board.Occ, S.Tray[slot], out r, out c)) { _finger.gameObject.SetActive(false); return; }
            _finger.gameObject.SetActive(true);
            Vector3 from = _tray.SlotWorld(slot);
            var sh = S.Tray[slot];
            Vector3 to = _board.Rt.TransformPoint(BoardView.CellPos(r, c) + new Vector2((sh.Width - 1) * BoardView.Cell * 0.5f, -(sh.Height - 1) * BoardView.Cell * 0.5f));
            Tween.Value(1.5f, k =>
            {
                if (_finger == null) return;
                float e = Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.25f));
                _finger.position = Vector3.Lerp(from, to, e);
                float s = 1f - 0.18f * Mathf.Sin(Mathf.Clamp01(k * 1.25f) * Mathf.PI);
                _finger.localScale = Vector3.one * s;
                var im = _finger.GetComponent<Image>();
                im.color = new Color(1, 1, 1, k > 0.85f ? (1f - k) / 0.15f * 0.85f : 0.85f);
            }, Ease.Linear, () => { if (_tutorialStage == 1) AnimateFinger(); }, 0.2f, _finger);
        }

        private void TutorialAdvance(int stage, string text)
        {
            if (_tutorialStage == 0 || stage <= _tutorialStage) return;
            _tutorialStage = stage;
            if (_tutorialText != null) { _tutorialText.text = text; Tween.Punch(_tutorialText.transform, 0.15f, 0.3f); }
            if (_finger != null) { Destroy(_finger.gameObject); _finger = null; }
            if (stage >= 3 && _tutorialText != null)
            {
                Save.Data.tutorialDone = true; Save.Commit();
                Ui.Later(3.2f, () => { if (_tutorialText != null) Destroy(_tutorialText.gameObject); });
            }
        }

        // ---------- moves ----------
        private void OnDropped(int slot, int r, int c)
        {
            if (_ended) return;
            var shape = S.Tray[slot];
            int colour = S.TrayColour[slot];
            var o = S.Place(slot, r, c);
            if (!o.Valid) { _tray.Show(S, false); return; }
            _placements++;
            Sfx.Drop();
            ulong placed = o.Place.PlacedMask;
            for (int i = 0; i < Bits.Cells; i++)
                if ((placed & (1UL << i)) != 0) _board.SetCell(i, colour + 1, false);
            _board.PopCells(placed);
            if (Fx.I != null) Fx.I.Ring(_board.CellWorld(r + shape.Height / 2, c + shape.Width / 2), Palette.Block(colour), 200f);
            if (_tutorialStage == 1) TutorialAdvance(2, "FILL A FULL ROW OR COLUMN TO CLEAR IT");

            float animTime = 0f;
            int lines = o.Place.Lines;
            if (lines > 0)
            {
                int combo = o.Score.Combo;
                _board.FlashLines(o.Place.RowMask, o.Place.ColMask, Palette.Block(colour));
                Sfx.Clear(lines, combo);
                animTime = _board.AnimateClear(o.Place.Cleared, r + shape.Height / 2, c + shape.Width / 2, combo, (w, idx) => GemCollected(w));
                Vector3 centre = _board.CellWorld(r + shape.Height / 2, c + shape.Width / 2);
                string praise = lines >= 5 ? "UNBELIEVABLE!" : (lines == 4 ? "AMAZING!" : (lines == 3 ? "EXCELLENT!" : (lines == 2 ? "GREAT!" : "")));
                if (Fx.I != null)
                {
                    Fx.I.Float(centre + new Vector3(0, 40, 0), "+" + o.Score.Total, 76 + lines * 8, Palette.Gold);
                    if (praise.Length > 0) Fx.I.Float(_board.Rt.position + new Vector3(0, 150, 0), praise, 100 + lines * 6, Palette.Block(colour + lines), 200f, 1.1f);
                    if (combo >= 2) Fx.I.Float(_board.Rt.position + new Vector3(0, -90, 0), "COMBO x" + combo, 84, Palette.Hex("#ff9f1c"), 160f, 1.1f);
                }
                if (lines >= 2 || combo >= 3) _board.Shake(5f + lines * 3f + combo);
                Economy.QuestAdd(0, lines);
                if (combo >= 3) Economy.QuestAdd(2, 1);
                if (o.Place.PerfectClear && Fx.I != null)
                {
                    Fx.I.Float(_board.Rt.position + new Vector3(0, 300, 0), "PERFECT CLEAR!", 120, Palette.Hex("#6fe3ff"), 220f, 1.6f);
                    Fx.I.Confetti(80); Sfx.Star(3);
                }
                if (_tutorialStage == 2) TutorialAdvance(3, "NICE! REACH THE GOAL BEFORE MOVES RUN OUT");
            }

            UpdateHud(false);
            if (_mode == Mode.Adventure && !_almostShown)
            {
                for (int g = 0; g < _def.Goals.Length; g++)
                    if (S.GoalFraction(g) >= 0.8f && S.GoalFraction(g) < 1f && !S.Finished) { _almostShown = true; Fx.I.Float(_board.Rt.position + new Vector3(0, 400, 0), "ALMOST THERE!", 84, Palette.Green, 120f, 1.2f); break; }
            }

            if (o.TrayRefilled) Ui.Later(Mathf.Max(0.12f, animTime * 0.4f), () => { if (this != null && !_ended) _tray.Show(S, true); });
            else _tray.Refresh();

            if (o.Won) Ui.Later(animTime + 0.35f, Win);
            else if (o.Lost) Ui.Later(animTime + 0.5f, Lose);
        }

        private void GemCollected(Vector3 world)
        {
            Save.Data.totalGems++;
            if (_goalIcon[0] == null) return;
            int gi = 0;
            for (int i = 0; i < _def.Goals.Length; i++) if (_def.Goals[i].Type == GoalType.Gems) gi = i;
            Sfx.Star(1);
            Fx.I.Fly(Sprites.Diamond(), world, _goalIcon[gi].transform.position, Palette.Hex("#6fe3ff"), 70, 0.6f, () =>
            {
                if (_goalIcon[gi] != null) Tween.Punch(_goalIcon[gi].transform, 0.4f, 0.25f);
            });
        }

        // ---------- boosters ----------
        private void UseBooster(int kind)
        {
            if (_ended || S == null || S.Finished) return;
            if (kind == 1 && _bombMode) { SetBombMode(false); return; }
            if (Economy.BoosterCount(kind) <= 0) { Popups.BoosterOffer(kind, RefreshBoosters); return; }
            switch (kind)
            {
                case 0:
                    if (!S.CanUndo) { App.I.Toast("Nothing to undo yet"); return; }
                    Economy.UseBooster(0);
                    S.Undo(); _board.Sync(S.Board); _tray.Show(S, false); UpdateHud(true); Sfx.Whoosh();
                    Monet.Log("booster_used", "kind", "undo");
                    break;
                case 1:
                    SetBombMode(true);
                    break;
                case 2:
                    Economy.UseBooster(2);
                    S.Shuffle(); _tray.Show(S, true); Sfx.Whoosh();
                    Monet.Log("booster_used", "kind", "shuffle");
                    break;
            }
            RefreshBoosters();
        }

        private void SetBombMode(bool on)
        {
            _bombMode = on;
            _bombOverlay.SetActive(on);
            _tray.InputEnabled = !on;
        }

        public void BombAt(Vector2 screen)
        {
            if (!_bombMode || _ended) return;
            Vector2 lp;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_board.Rt, screen, null, out lp)) return;
            Vector2 g = _board.LocalToGrid(lp);
            int r = Mathf.FloorToInt(g.x), c = Mathf.FloorToInt(g.y);
            if (r < 0 || c < 0 || r >= Bits.N || c >= Bits.N) return;
            ulong cleared; int gems;
            if (!S.UseBomb(r, c, out cleared, out gems)) { App.I.Toast("Pick a spot with blocks"); return; }
            Economy.UseBooster(1);
            SetBombMode(false);
            RefreshBoosters();
            Sfx.Bomb();
            Vector3 w = _board.CellWorld(r, c);
            if (Fx.I != null) { Fx.I.Ring(w, Palette.Hex("#ff8a34"), 520f); Fx.I.Flash(w, Color.white, 300f); Fx.I.Burst(w, Palette.Hex("#ff8a34"), 24, 800f, 34f); }
            _board.Shake(14f);
            float t = _board.AnimateClear(cleared, r, c, 1, (wp, idx) => GemCollected(wp));
            Monet.Log("booster_used", "kind", "bomb");
            UpdateHud(false);
            _tray.Refresh();
            if (S.Finished && S.Won) Ui.Later(t + 0.35f, Win);
        }

        // ---------- end of game ----------
        private void Win()
        {
            if (_ended) return;
            _ended = true; _tray.InputEnabled = false;
            Sfx.Win(); Fx.I.Confetti(110);
            int stars = S.LastWinStars;
            var d = Save.Data;
            int prevStars = d.stars[_levelIndex - 1];
            bool first = prevStars == 0;
            int reward = first ? _def.RewardCoins : Mathf.Max(20, _def.RewardCoins / 5);
            d.stars[_levelIndex - 1] = Mathf.Max(prevStars, stars);
            d.bestLevelScore[_levelIndex - 1] = Mathf.Max(d.bestLevelScore[_levelIndex - 1], S.Score);
            if (_levelIndex >= d.unlockedLevel && _levelIndex < Adventure.LevelCount) d.unlockedLevel = _levelIndex + 1;
            d.totalLines += S.Keeper.TotalLines; d.gamesPlayed++; d.bestCombo = Mathf.Max(d.bestCombo, S.Keeper.BestCombo);
            d.coins += reward;
            bool chest; int gained = Mathf.Max(0, stars - prevStars);
            Economy.AddStars(gained, out chest);
            if (!d.tutorialDone) d.tutorialDone = true;
            Economy.QuestAdd(1, 1);
            Save.Commit();
            Monet.Log("level_win", "level", _levelIndex, "stars", stars, "moves_left", S.MovesLeft, "score", S.Score);

            var p = Popup.Create(new Vector2(920, 1180), "LEVEL " + _levelIndex + " CLEAR!", false, Palette.Green);
            float baseY = 380;
            var starsRoot = Ui.Rect(p.Card, "stars"); starsRoot.sizeDelta = new Vector2(600, 200); starsRoot.Pos(0, baseY);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var s = Ui.Img(starsRoot, Sprites.Star(), i < stars ? Palette.Gold : new Color(0.1f, 0.06f, 0.3f, 0.6f), "s" + i);
                float sz = i == 1 ? 200 : 170;
                Ui.At(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 200, i == 1 ? 30 : 0), new Vector2(sz, sz));
                s.rectTransform.localScale = Vector3.zero;
                if (i < stars)
                    Ui.Later(0.45f + 0.35f * i, () =>
                    {
                        if (s == null) return;
                        Tween.ScaleTo(s.rectTransform, Vector3.one, 0.35f, Ease.OutBack);
                        Sfx.Star(k); Fx.I.Burst(s.rectTransform.position, Palette.Gold, 14, 600f, 30f);
                    });
                else s.rectTransform.localScale = Vector3.one;
            }
            Ui.Label(p.Card, "SCORE", 40, Palette.Alpha(Color.white, 0.8f), TextAnchor.MiddleCenter).Pos(0, 200);
            Ui.Label(p.Card, Ui.Num(S.Score), 100, Palette.Gold, TextAnchor.MiddleCenter).Pos(0, 120);
            var cr = Ui.RoundImg(p.Card, new Color(0, 0, 0, 0.3f), 40, "reward"); cr.rectTransform.sizeDelta = new Vector2(420, 100); cr.Pos(0, 10);
            var ci = Ui.Img(cr.transform, Sprites.Coin(), Palette.Gold, "c"); Ui.At(ci.rectTransform, new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(76, 76));
            Ui.LabelAt(cr.transform, "+" + reward, 60, Color.white, new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(260, 80));
            // chest progress
            RectTransform cf;
            var bar = Widgets.Bar(p.Card, new Vector2(560, 46), new Color(0, 0, 0, 0.4f), Palette.Hex("#ff9f1c"), out cf); bar.Pos(0, -100);
            Widgets.SetBar(cf, 560, d.chestProgress / (float)Economy.ChestStars);
            Ui.LabelAt(bar.transform, "STAR CHEST  " + Mathf.Min(d.chestProgress, Economy.ChestStars) + "/" + Economy.ChestStars, 30, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540, 44));
            if (chest)
            {
                d.chestProgress -= Economy.ChestStars; d.coins += Economy.ChestCoins; d.boosterBomb++; Save.Commit();
                Ui.Later(1.4f, () => { App.I.Toast("Star chest opened: +" + Economy.ChestCoins + " coins & a bomb!"); Sfx.Coin(); });
            }
            // double coins via video
            if (Monet.RewardedReady("win_double"))
            {
                Button dbl = null;
                dbl = Ui.Btn(p.Card, "x2 COINS  (VIDEO)", Palette.Hex("#ff9f1c"), Palette.Hex("#c06a00"), new Vector2(660, 110), () =>
                {
                    Monet.Rewarded("win_double", () => { Economy.AddCoins(reward); Sfx.Coin(); App.I.Toast("+" + reward + " bonus coins!"); dbl.gameObject.SetActive(false); });
                }, 46);
                dbl.Pos(0, -230);
            }
            int next = Mathf.Min(_levelIndex + 1, Adventure.LevelCount);
            Ui.Btn(p.Card, "NEXT", Palette.Green, Palette.GreenDark, new Vector2(420, 130), () =>
            {
                p.Close(true);
                Monet.MaybeInterstitial(() => { App.I.ShowMap(); Ui.Later(0.5f, () => LevelUi.Open(next)); });
            }, 70).Pos(150, -400);
            Ui.IconBtn(p.Card, Sprites.Undo(), Palette.Blue, Palette.BlueDark, 124, () => { p.Close(true); App.I.StartLevel(_levelIndex); }).Pos(-250, -400);
        }

        private void Lose()
        {
            if (_ended) return;
            _tray.InputEnabled = false;
            Sfx.Lose();
            if (_mode == Mode.Adventure) LoseAdventure(); else LoseEndless();
        }

        private void LoseAdventure()
        {
            bool noMoves = S.MovesLeft <= 0;
            var p = Popup.Create(new Vector2(920, 1060), noMoves ? "OUT OF MOVES!" : "NO SPACE LEFT!", false, Palette.Red);
            for (int i = 0; i < _def.Goals.Length; i++)
            {
                var g = _def.Goals[i];
                var row = Ui.RoundImg(p.Card, new Color(0, 0, 0, 0.3f), 34, "row"); row.rectTransform.sizeDelta = new Vector2(700, 90); row.Pos(0, 330 - i * 100);
                var ic = Ui.Img(row.transform, LevelUi.GoalSprite(g), LevelUi.GoalColour(g), "ic"); Ui.At(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(60, 60));
                Ui.LabelAt(row.transform, Adventure.ShortGoal(g) + "  " + Mathf.Min(S.GoalProgress[i], g.Target) + "/" + g.Target, 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(520, 70));
            }
            int cost = Economy.ReviveCost * (S.Revives + 1);
            string vtxt = noMoves ? "+" + Economy.ExtraMovesAmount + " MOVES  (VIDEO)" : "CONTINUE  (VIDEO)";
            Action revive = () =>
            {
                p.Close(true);
                ReviveNow();
            };
            Ui.Btn(p.Card, vtxt, Palette.Green, Palette.GreenDark, new Vector2(740, 130), () =>
            {
                Monet.Rewarded(noMoves ? "extra_moves" : "revive", revive);
            }, 54).Pos(0, 60);
            Ui.Btn(p.Card, (noMoves ? "+" + Economy.ExtraMovesAmount + " MOVES  " : "CONTINUE  ") + cost + " COINS", Palette.Gold, Palette.GoldDark, new Vector2(740, 130), () =>
            {
                if (!Economy.Spend(cost)) { Popups.Shop(); return; }
                revive();
            }, 48).Pos(0, -110);
            Ui.Btn(p.Card, "GIVE UP", Palette.Hex("#7a76a8"), Palette.Hex("#4c4880"), new Vector2(380, 100), () =>
            {
                _ended = true;
                Economy.LoseHeart(); Save.Data.gamesPlayed++; Save.Commit();
                Monet.Log("level_fail", "level", _levelIndex, "score", S.Score);
                p.Close(true);
                Monet.MaybeInterstitial(() => App.I.ShowMap());
            }, 44).Pos(-190, -300);
            Ui.Btn(p.Card, "RETRY", Palette.Blue, Palette.BlueDark, new Vector2(380, 100), () =>
            {
                _ended = true;
                Economy.LoseHeart(); Save.Data.gamesPlayed++; Save.Commit();
                p.Close(true);
                Economy.TickHearts();
                if (Save.Data.hearts <= 0) { App.I.ShowMap(); Ui.Later(0.6f, () => Popups.NoHearts(null)); return; }
                App.I.StartLevel(_levelIndex);
            }, 44).Pos(190, -300);
            Ui.Label(p.Card, "Leaving costs 1 heart", 30, Palette.Alpha(Color.white, 0.65f), TextAnchor.MiddleCenter, false).Pos(0, -400);
        }

        private void ReviveNow()
        {
            ulong band = S.Revive(Economy.ExtraMovesAmount);
            _ended = false; _tray.InputEnabled = true;
            _board.Sync(S.Board);
            _tray.Show(S, true);
            UpdateHud(true);
            Sfx.Star(2); Fx.I.Confetti(30);
            Fx.I.Float(_board.Rt.position, "BACK IN!", 110, Palette.Green, 160f, 1.2f);
            Monet.Log("revive", "mode", _mode.ToString());
        }

        private void LoseEndless()
        {
            var d = Save.Data;
            bool daily = _mode == Mode.Daily;
            int best = daily ? d.dailyBest : d.classicBest;
            bool newBest = S.Score > best;
            var p = Popup.Create(new Vector2(920, 1120), "GAME OVER", false, Palette.Red);
            Ui.Label(p.Card, newBest ? "NEW BEST!" : "SCORE", 56, newBest ? Palette.Gold : Palette.Alpha(Color.white, 0.85f), TextAnchor.MiddleCenter).Pos(0, 330);
            Ui.Label(p.Card, Ui.Num(S.Score), 150, Palette.Gold, TextAnchor.MiddleCenter).Pos(0, 220);
            Ui.Label(p.Card, "BEST  " + Ui.Num(Mathf.Max(best, S.Score)) + "    LINES  " + S.Keeper.TotalLines + "    COMBO  x" + S.Keeper.BestCombo, 36, Color.white, TextAnchor.MiddleCenter).Pos(0, 100);
            if (newBest) Fx.I.Confetti(70);
            int coins = Mathf.Min(300, S.Score / 6);
            if (daily && d.dailyDone != Save.Today) { coins += 100; d.dailyDone = Save.Today; }
            d.coins += coins;
            if (daily) d.dailyBest = Mathf.Max(d.dailyBest, S.Score); else d.classicBest = Mathf.Max(d.classicBest, S.Score);
            d.totalLines += S.Keeper.TotalLines; d.gamesPlayed++; d.bestCombo = Mathf.Max(d.bestCombo, S.Keeper.BestCombo);
            Economy.QuestAdd(1, 1);
            Save.Commit();
            Monet.Log("game_over", "mode", _mode.ToString(), "score", S.Score);
            var cr = Ui.RoundImg(p.Card, new Color(0, 0, 0, 0.3f), 40, "reward"); cr.rectTransform.sizeDelta = new Vector2(340, 90); cr.Pos(0, 0);
            var ci = Ui.Img(cr.transform, Sprites.Coin(), Palette.Gold, "c"); Ui.At(ci.rectTransform, new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(66, 66));
            Ui.LabelAt(cr.transform, "+" + coins, 52, Color.white, new Vector2(0.5f, 0.5f), new Vector2(30, 0), new Vector2(220, 70));
            Ui.Btn(p.Card, "REVIVE  (VIDEO)", Palette.Green, Palette.GreenDark, new Vector2(740, 130), () =>
            {
                Monet.Rewarded("revive", () => { p.Close(true); ReviveNow(); });
            }, 54).Pos(0, -140);
            Ui.Btn(p.Card, "PLAY AGAIN", Palette.Blue, Palette.BlueDark, new Vector2(400, 110), () =>
            {
                p.Close(true); _ended = true;
                Monet.MaybeInterstitial(() => { if (daily) App.I.StartDaily(); else App.I.StartClassic(); });
            }, 44).Pos(-210, -330);
            Ui.Btn(p.Card, "HOME", Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), new Vector2(400, 110), () =>
            {
                p.Close(true); _ended = true;
                Monet.MaybeInterstitial(() => App.I.ShowHome());
            }, 44).Pos(210, -330);
            _ended = true;
        }

        /// <summary>CI / demo helper: plays one sensible move as if the player had dragged it.</summary>
        public bool AutoMove()
        {
            if (_ended || S == null || S.Finished) return false;
            int bestSlot = -1, br = 0, bc = 0, bs = int.MinValue;
            for (int i = 0; i < GameSession.TraySize; i++)
            {
                if (S.Used[i] || S.Tray[i] == null) continue;
                int r, c;
                if (!Solver.BestPlacement(S.Board.Occ, S.Tray[i], out r, out c)) continue;
                ulong after = S.Board.Occ | S.Tray[i].MaskAt(r, c);
                ulong clr = Bits.ClearedMask(after);
                int sc = Bits.PopCount(clr) * 10 + ((clr & S.Board.Gems) != 0 ? 30 : 0);
                if (sc > bs) { bs = sc; bestSlot = i; br = r; bc = c; }
            }
            if (bestSlot < 0) return false;
            _tray.RemovePiece(bestSlot);
            OnDropped(bestSlot, br, bc);
            return true;
        }

        public bool Ended { get { return _ended; } }

        // ---------- pause ----------
        private void PausePopup()
        {
            if (_ended) return;
            var p = Popup.Create(new Vector2(840, 940), "PAUSED", true, Palette.Purple);
            Ui.Btn(p.Card, "RESUME", Palette.Green, Palette.GreenDark, new Vector2(620, 130), () => p.Close(), 64).Pos(0, 220);
            Ui.Btn(p.Card, "RESTART", Palette.Blue, Palette.BlueDark, new Vector2(620, 120), () =>
            {
                p.Close(true);
                if (_mode == Mode.Adventure) App.I.StartLevel(_levelIndex); else if (_mode == Mode.Daily) App.I.StartDaily(); else App.I.StartClassic();
            }, 56).Pos(0, 50);
            Ui.Btn(p.Card, "SETTINGS", Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), new Vector2(620, 120), () => Popups.Settings(null), 56).Pos(0, -120);
            Ui.Btn(p.Card, "QUIT", Palette.Red, Palette.RedDark, new Vector2(620, 120), () =>
            {
                p.Close(true);
                if (_mode == Mode.Adventure) App.I.ShowMap(); else App.I.ShowHome();
            }, 56).Pos(0, -290);
        }

        public override void OnBack()
        {
            if (_bombMode) { SetBombMode(false); return; }
            PausePopup();
        }
    }
}
