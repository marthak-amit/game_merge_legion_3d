using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Save;
using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>Stack-based screen navigation for the Main scene. Screens are built in code by <see cref="ScreenFactory"/>.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        private readonly Dictionary<ScreenId, UIScreen> _byId = new Dictionary<ScreenId, UIScreen>();
        private readonly Stack<ScreenId> _stack = new Stack<ScreenId>();

        public static UIManager Instance { get; private set; }

        public ScreenId Current => _stack.Count > 0 ? _stack.Peek() : ScreenId.None;
        public int Depth => _stack.Count;
        public Canvas Canvas { get; private set; }

        private void Awake()
        {
            Instance = this;
            GameBootstrap.EnsureServices();
            string lang = ServiceLocator.TryGet<SaveService>(out var save) ? save.Data.settings.language : "en";
            Loc.Load(lang);
            UIKit.EnsureEventSystem();

            Canvas = GetComponent<Canvas>();
            ScreenFactory.CreateAll(transform, this);
            foreach (var s in _byId.Values) s.Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (_stack.Count == 0) Push(ScreenId.Home);
        }

        public void Register(UIScreen screen) => _byId[screen.Id] = screen;

        public T Get<T>(ScreenId id) where T : UIScreen => _byId.TryGetValue(id, out var s) ? s as T : null;

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

        private void Update()
        {
            // Android back button / Escape
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Pop();
        }
    }
}
