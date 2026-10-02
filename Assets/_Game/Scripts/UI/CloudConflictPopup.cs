using System;
using MergeLegion.Save;
using TMPro;
using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>"Cloud save vs this device" prompt shown when both have progress (section 5: higher progress wins, with a prompt).</summary>
    public static class CloudConflictPopup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => Services.CloudSyncService.DefaultConflictPrompt = Show;

        public static void Show(SaveData local, SaveData cloud, Action<MergeChoice> choose)
        {
            PopupManager.Show(() =>
            {
                var popup = PopupManager.Create(Loc.Get("cloud.title"), new Vector2(940, 1000));
                var c = popup.Content;
                var msg = UIKit.Label(c, Loc.Get("cloud.message"), 38, UIKit.TextDim);
                UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(840, 130));

                bool cloudBetter = cloud.ProgressScore > local.ProgressScore;
                Card(c, Loc.Get("cloud.on_cloud"), cloud, cloudBetter, new Vector2(0.5f, 1f), -170);
                Card(c, Loc.Get("cloud.on_device"), local, !cloudBetter, new Vector2(0.5f, 1f), -170 - 250);

                var useCloud = UIKit.Btn(c, Loc.Get("cloud.use_cloud"), cloudBetter ? UIKit.Good : UIKit.PanelLight,
                    () => { popup.Close(); choose(MergeChoice.UseCloud); }, new Vector2(840, 110), 44);
                UIKit.Place((RectTransform)useCloud.transform, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(840, 110));
                var keep = UIKit.Btn(c, Loc.Get("cloud.keep_device"), cloudBetter ? UIKit.PanelLight : UIKit.Good,
                    () => { popup.Close(); choose(MergeChoice.UseLocal); }, new Vector2(840, 110), 44);
                UIKit.Place((RectTransform)keep.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(840, 110));
                return popup;
            }, true);
        }

        private static void Card(RectTransform parent, string title, SaveData d, bool recommended, Vector2 anchor, float y)
        {
            var img = UIKit.PanelImage(parent, recommended ? new Color(0.2f, 0.4f, 0.25f, 1f) : UIKit.PanelLight, "Card");
            UIKit.Place(img.rectTransform, anchor, new Vector2(0, y), new Vector2(840, 220));
            var t = UIKit.Label(img.transform, title + (recommended ? "  *" : ""), 40, UIKit.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Stretch(t.rectTransform, 24, 0, 24, 14);
            var body = UIKit.Label(img.transform,
                Loc.Format("cloud.summary", d.highestCampaignLevel, d.gems, d.endlessBestWave), 36, Color.white, TextAlignmentOptions.BottomLeft);
            UIKit.Stretch(body.rectTransform, 24, 14, 24, 60);
        }
    }
}
