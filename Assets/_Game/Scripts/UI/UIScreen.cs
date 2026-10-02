using UnityEngine;

namespace MergeLegion.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] private ScreenId id;
        private CanvasGroup _group;

        public ScreenId Id => id;
        public bool IsVisible { get; private set; }

        private CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());

        public void Show()
        {
            gameObject.SetActive(true);
            Group.alpha = 1f;
            Group.interactable = true;
            Group.blocksRaycasts = true;
            IsVisible = true;
            OnShown();
        }

        public void Hide()
        {
            if (!IsVisible && !gameObject.activeSelf) return;
            IsVisible = false;
            OnHidden();
            Group.interactable = false;
            Group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
