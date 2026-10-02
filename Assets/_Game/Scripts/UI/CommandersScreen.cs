using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Meta;
using MergeLegion.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Commanders / heroes (section 1.3): unlock and upgrade with shards, equip one.</summary>
    public sealed class CommandersScreen : MenuScreen
    {
        private sealed class Card
        {
            public CommanderData Data;
            public Image Background;
            public TMP_Text Title, Detail, Shards;
            public Image ShardBar;
            public Button Primary, Equip;
        }

        private readonly List<Card> _cards = new List<Card>();

        protected override void BuildContent(RectTransform c)
        {
            RowKit.ScrollArea(c, 18, 0, out var list);
            foreach (var cmd in GameDatabase.Instance.commanders)
            {
                var card = new Card { Data = cmd };
                var row = UIKit.ListRow(list, 380);
                card.Background = row.GetComponent<Image>();
                RowKit.Dot(row, cmd.tint, 24, 24, 110);
                card.Title = RowKit.Left(row, "", 42, Color.white, 160, 18, 560, 60, FontStyles.Bold);
                card.Detail = RowKit.Left(row, "", 30, UIKit.TextDim, 160, 80, 600, 110);
                card.Shards = RowKit.Left(row, "", 30, UIKit.Gold, 160, 200, 500, 44);
                RowKit.Bar(row, new Color(0.9f, 0.5f, 0.85f), 160, 250, 480, 22, out card.ShardBar);

                var captured = cmd;
                card.Primary = RowKit.RightButton(row, "", UIKit.Good, () => OnPrimary(captured), new Vector2(300, 100), 34, 16, 130);
                card.Equip = RowKit.RightButton(row, Loc.Get("common.equip"), UIKit.Blue, () => OnEquip(captured), new Vector2(300, 100), 34, 16, 250);
                _cards.Add(card);
                TutorialTargets.Register("cmd_" + cmd.id, (RectTransform)card.Primary.transform);
            }
        }

        private void OnPrimary(CommanderData cmd)
        {
            var svc = ServiceLocator.Get<CommanderService>();
            var result = svc.IsUnlocked(cmd.id) ? svc.Upgrade(cmd.id) : svc.Unlock(cmd.id);
            if (result == CommanderResult.Ok)
            {
                Sfx.Play(SfxId.Reward);
                Haptics.Medium();
                var card = _cards.Find(x => x.Data == cmd);
                Tween.Punch(card.Background.transform, 0.04f, 0.3f);
                UIKit.Flash(card.Background, new Color(1f, 0.95f, 0.6f));
                if (svc.GetLevel(cmd.id) == 1 && svc.EquippedId != cmd.id && !svc.IsUnlocked(svc.EquippedId)) svc.Equip(cmd.id);
            }
            else if (result == CommanderResult.NotEnoughShards)
            {
                Toast.Show(Loc.Get("toast.not_enough_shards"));
                Sfx.Play(SfxId.Error);
            }
            Refresh();
        }

        private void OnEquip(CommanderData cmd)
        {
            if (ServiceLocator.Get<CommanderService>().Equip(cmd.id)) Sfx.Play(SfxId.Click);
            Refresh();
        }

        protected override void Refresh()
        {
            var svc = ServiceLocator.Get<CommanderService>();
            foreach (var card in _cards)
            {
                var cmd = card.Data;
                int level = svc.GetLevel(cmd.id);
                bool unlocked = level > 0;
                bool equipped = svc.EquippedId == cmd.id;
                long shards = svc.Shards(cmd.id);

                card.Background.color = equipped ? new Color(0.2f, 0.32f, 0.5f, 1f) : UIKit.Panel;
                card.Title.text = Loc.Get(cmd.nameKey) + (unlocked ? "   Lv " + level : "");
                float scale = unlocked ? svc.Scale(cmd, level) : 1f;
                card.Detail.text = Loc.Get(cmd.passiveKey) + " +" + Mathf.RoundToInt(cmd.passiveValue * scale * 100f) + "%\n"
                                   + Loc.Get(cmd.skillKey) + "  (" + Mathf.RoundToInt(cmd.cooldown) + "s)";

                long need = unlocked ? (level >= cmd.maxLevel ? 0 : svc.UpgradeCost(cmd.id)) : cmd.unlockShards;
                card.Shards.text = need > 0 ? Loc.Format("cmd.shards", shards, need) : Loc.Get("common.max");
                card.ShardBar.fillAmount = need > 0 ? Mathf.Clamp01((float)shards / need) : 1f;

                if (!unlocked)
                {
                    UIKit.SetButtonText(card.Primary, Loc.Get("common.unlock"));
                    card.Primary.interactable = shards >= need;
                }
                else if (level >= cmd.maxLevel)
                {
                    UIKit.SetButtonText(card.Primary, Loc.Get("common.max"));
                    card.Primary.interactable = false;
                }
                else
                {
                    UIKit.SetButtonText(card.Primary, Loc.Get("common.upgrade"));
                    card.Primary.interactable = shards >= need;
                }
                card.Primary.GetComponent<Image>().color = card.Primary.interactable ? UIKit.Good : UIKit.Disabled;

                card.Equip.gameObject.SetActive(unlocked);
                UIKit.SetButtonText(card.Equip, equipped ? Loc.Get("common.equipped") : Loc.Get("common.equip"));
                card.Equip.interactable = !equipped;
            }
        }
    }
}
