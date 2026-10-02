using TMPro;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>Handle on a spawned unit model (primitive or swapped-in prefab).</summary>
    public sealed class UnitVisual : MonoBehaviour
    {
        public float HoverHeight;
        public float BaseScale = 1f;
        public int Line;
        public int Level;
        public Color Tint = Color.white;
        public TextMeshPro Label;

        public void SetLabel(string text, Quaternion facing)
        {
            if (Label == null)
            {
                var go = new GameObject("Label");
                go.transform.SetParent(transform, false);
                Label = go.AddComponent<TextMeshPro>();
                Label.alignment = TextAlignmentOptions.Center;
                Label.fontSize = 6f;
                Label.color = Color.white;
                Label.sortingOrder = 10;
            }
            Label.text = text;
            Label.transform.localPosition = new Vector3(0f, 1.55f / Mathf.Max(0.1f, BaseScale), 0f);
            Label.transform.rotation = facing;
            Label.transform.localScale = Vector3.one / Mathf.Max(0.1f, BaseScale);
        }

        public void HideLabel()
        {
            if (Label != null) Label.gameObject.SetActive(false);
        }
    }
}
