using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Save;
using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>Stack-based screen navigation. Screens are assigned in the scene by the scene generator.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private List<UIScreen> screens = new List<UIScreen>();

        private readonly Dictionary<ScreenId, UIScreen> _byId = new Dictionary<ScreenId, UIScreen>();
        private readonly Stack<ScreenId> _stack = new Stack<ScreenId>();

        public ScreenId Current => _stack.Count > 0 ? _stack.Peek() : ScreenId.None;
        public int Depth => _stack.Count;

        private void Awake()
        {
            string lang = ServiceLocator.TryGet<SaveService>(out var save) ? save.Data.settings.language : "en";
            Loc.Load(lang);

            foreach (var s in screens)
            {
                if (s == null) continue;
                _byId[s.Id] = s;
                s.Hide();
            }
        }

        private void Start()
        {
            if (_stack.Count == 0) Push(ScreenId.Home);
        }

        public void Push(ScreenId id)
        {
            if (!_byId.TryGetValue(id, out var next))
            {
                Debug.LogError("[UI] No screen registered for " + id);
                return;
            }
            if (_stack.Count > 0) _byId[_stack.Peek()].Hide();
            _stack.Push(id);
            next.Show();
            EventBus.Publish(new ScreenChangedEvent(id));
        }

        public bool Pop()
        {
            if (_stack.Count <= 1) return false;
            _byId[_stack.Pop()].Hide();
            var prev = _stack.Peek();
            _byId[prev].Show();
            EventBus.Publish(new ScreenChangedEvent(prev));
            return true;
        }

        /// <summary>Clears the stack and shows a single root screen.</summary>
        public void Replace(ScreenId id)
        {
            while (_stack.Count > 0) _byId[_stack.Pop()].Hide();
            Push(id);
        }
    }
}
