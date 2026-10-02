using System;
using System.Collections.Generic;

namespace MergeLegion.Core
{
    /// <summary>
    /// Type-safe pub/sub using struct events. No allocation on Publish once subscribers are registered.
    /// </summary>
    public static class EventBus
    {
        private static readonly List<Action> ClearActions = new List<Action>();

        internal static void RegisterClear(Action clear) => ClearActions.Add(clear);

        /// <summary>Removes every subscriber of every event type (tests, full restarts).</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < ClearActions.Count; i++) ClearActions[i]();
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct => Channel<T>.Subscribe(handler);
        public static void Unsubscribe<T>(Action<T> handler) where T : struct => Channel<T>.Unsubscribe(handler);
        public static void Publish<T>(in T evt) where T : struct => Channel<T>.Publish(evt);

        private static class Channel<T> where T : struct
        {
            private static readonly List<Action<T>> Handlers = new List<Action<T>>();
            private static int _publishDepth;
            private static bool _dirty;

            static Channel() => RegisterClear(Clear);

            public static void Subscribe(Action<T> handler)
            {
                if (handler == null || Handlers.Contains(handler)) return;
                Handlers.Add(handler);
            }

            public static void Unsubscribe(Action<T> handler)
            {
                int i = Handlers.IndexOf(handler);
                if (i < 0) return;
                if (_publishDepth > 0)
                {
                    Handlers[i] = null; // compact after publish to keep iteration stable
                    _dirty = true;
                }
                else Handlers.RemoveAt(i);
            }

            public static void Publish(in T evt)
            {
                _publishDepth++;
                try
                {
                    int count = Handlers.Count; // handlers added during publish fire next time
                    for (int i = 0; i < count; i++)
                    {
                        var h = Handlers[i];
                        if (h == null) continue;
                        try { h(evt); }
                        catch (Exception e) { UnityEngine.Debug.LogException(e); }
                    }
                }
                finally
                {
                    _publishDepth--;
                    if (_publishDepth == 0 && _dirty)
                    {
                        Handlers.RemoveAll(h => h == null);
                        _dirty = false;
                    }
                }
            }

            private static void Clear()
            {
                Handlers.Clear();
                _publishDepth = 0;
                _dirty = false;
            }
        }
    }
}
