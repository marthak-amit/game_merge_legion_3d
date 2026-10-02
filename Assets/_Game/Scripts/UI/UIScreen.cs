using UnityEngine;

namespace MergeLegion.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        private CanvasGroup _group;
        private bool _built;

        public ScreenId Id { get; private set; }
        public bool IsVisible { get; private set; }

        private CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());

        /// <summary>Creates a full-stretch screen object under <paramref name="parent"/> and builds its widgets.</summary>
        public static T Create<T>(Transform parent, ScreenId id) where T : UIScreen
        {
            var go = new GameObject(id + "Screen", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            UIKit.Stretch((RectTransform)go.transform);
            var screen = go.AddComponent<T>();
            screen.Id = id;
            screen.EnsureBuilt();
            return screen;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            Build();
        }

        /// <summary>Construct child widgets once. Called when the screen is created.</summary>
        protected abstract void Build();

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
