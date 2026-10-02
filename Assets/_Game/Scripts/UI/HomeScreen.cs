using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Phase 1: empty shell. Later phases add the battle button flow, castle, chests and bottom nav.</summary>
    public sealed class HomeScreen : UIScreen
    {
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField] private Button battleButton;

        protected override void OnShown()
        {
            if (versionLabel != null) versionLabel.text = Loc.Format("home.version", Application.version);
            if (battleButton != null) battleButton.interactable = false; // enabled in Phase 3
        }
    }
}
