using TMPro;
using UnityEngine;

namespace MergeLegion.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        public string Key
        {
            get => key;
            set { key = value; Refresh(); }
        }

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key)) return;
            GetComponent<TMP_Text>().text = Loc.Get(key);
        }
    }
}
