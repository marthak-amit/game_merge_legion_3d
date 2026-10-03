using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBloom.Core
{
    public enum Ease { Linear, OutQuad, OutCubic, OutBack, InOutSine }

    /// <summary>
    /// Small dependency-free tween runner (DOTween is an Asset Store package, so it is not assumed).
    /// Not used inside battle update loops; meant for UI and juice.
    /// </summary>
    public sealed class Tween : MonoBehaviour
    {
        private sealed class Item
        {
            public Action<float> Apply;
            public Action Done;
            public float Duration, Time, Delay;
            public Ease Ease;
            public UnityEngine.Object Owner;
            public bool HasOwner;
            public bool Unscaled;
        }

        private static Tween _instance;
        private static readonly List<Item> Items = new List<Item>(64);
        private static readonly Stack<Item> Pool = new Stack<Item>(64);

        private static Tween Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Tween]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Tween>();
                }
                return _instance;
            }
        }

        public static void Value(float duration, Action<float> onUpdate, Ease ease = Ease.OutQuad, Action onDone = null,
            float delay = 0f, UnityEngine.Object owner = null, bool unscaled = true)
        {
            var _ = Instance;
            var item = Pool.Count > 0 ? Pool.Pop() : new Item();
            item.Apply = onUpdate;
            item.Done = onDone;
            item.Duration = Mathf.Max(0.0001f, duration);
            item.Time = 0f;
            item.Delay = delay;
            item.Ease = ease;
            item.Owner = owner;
            item.HasOwner = owner != null;
            item.Unscaled = unscaled;
            Items.Add(item);
        }

        public static void Kill(UnityEngine.Object owner)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i].HasOwner && Items[i].Owner == owner) Recycle(i);
            }
        }

        public static void MoveTo(Transform t, Vector3 target, float duration, Ease ease = Ease.OutCubic, Action onDone = null)
        {
            Kill(t);
            Vector3 from = t.position;
            Value(duration, k => t.position = Vector3.LerpUnclamped(from, target, k), ease, onDone, 0f, t);
        }

        public static void ScaleTo(Transform t, Vector3 target, float duration, Ease ease = Ease.OutBack, float delay = 0f)
        {
            Vector3 from = t.localScale;
            Value(duration, k => t.localScale = Vector3.LerpUnclamped(from, target, k), ease, null, delay, t);
        }

        /// <summary>Scale pulse: grows by 'amount' and returns to the original scale.</summary>
        public static void Punch(Transform t, float amount = 0.25f, float duration = 0.25f)
        {
            Vector3 baseScale = t.localScale;
            Value(duration, k => t.localScale = baseScale * (1f + amount * Mathf.Sin(k * Mathf.PI)), Ease.Linear,
                () => { if (t != null) t.localScale = baseScale; }, 0f, t);
        }

        private void Update()
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var it = Items[i];
                if (it.HasOwner && it.Owner == null) { Recycle(i); continue; }

                float dt = it.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                if (it.Delay > 0f) { it.Delay -= dt; continue; }

                it.Time += dt;
                float k = Mathf.Clamp01(it.Time / it.Duration);
                it.Apply(Evaluate(it.Ease, k));
                if (k < 1f) continue;

                var done = it.Done;
                Recycle(i);
                done?.Invoke();
            }
        }

        private static void Recycle(int index)
        {
            var it = Items[index];
            int last = Items.Count - 1;
            Items[index] = Items[last];
            Items.RemoveAt(last);
            it.Apply = null; it.Done = null; it.Owner = null;
            Pool.Push(it);
        }

        public static float Evaluate(Ease ease, float k)
        {
            switch (ease)
            {
                case Ease.OutQuad: return 1f - (1f - k) * (1f - k);
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - k, 3f);
                case Ease.OutBack:
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float x = k - 1f;
                    return 1f + c3 * x * x * x + c1 * x * x;
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * k) - 1f) * 0.5f;
                default: return k;
            }
        }
    }
}
