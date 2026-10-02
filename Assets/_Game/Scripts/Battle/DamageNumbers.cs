using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>Pooled floating combat text. Uses a ring of preallocated TextMeshPro objects and cached number strings.</summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private const int PoolSize = 36;

        private struct Entry { public TextMeshPro Text; public float Age; public float Life; public bool Active; public Vector3 Velocity; }

        private readonly Entry[] _entries = new Entry[PoolSize];
        private readonly Dictionary<int, string> _strings = new Dictionary<int, string>(512);
        private int _next;
        private Quaternion _facing = Quaternion.identity;
        private float _perSecondBudget = 40f;
        private float _budget;

        public void Init(Quaternion cameraRotation)
        {
            _facing = cameraRotation;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Num" + i);
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshPro>();
                t.alignment = TextAlignmentOptions.Center;
                t.fontSize = 5f;
                t.fontStyle = FontStyles.Bold;
                t.sortingOrder = 20;
                go.transform.rotation = _facing;
                go.SetActive(false);
                _entries[i].Text = t;
            }
        }

        public void Show(Vector3 position, float amount, Color color)
        {
            if (_budget < 1f) return; // cap how many numbers spawn per second
            _budget -= 1f;

            int idx = _next;
            _next = (_next + 1) % PoolSize;
            var text = _entries[idx].Text;
            text.text = Format(amount);
            text.color = color;
            text.transform.position = position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.2f, 0.2f));
            text.transform.localScale = Vector3.one * 0.6f;
            text.gameObject.SetActive(true);
            _entries[idx].Age = 0f;
            _entries[idx].Life = 0.75f;
            _entries[idx].Active = true;
            _entries[idx].Velocity = new Vector3(0f, 2.2f, 0f);
        }

        private string Format(float amount)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (_strings.TryGetValue(n, out var s)) return s;
            if (_strings.Count > 3000) _strings.Clear();
            s = n >= 1000000 ? (n / 1000000f).ToString("0.#") + "M" : n >= 10000 ? (n / 1000f).ToString("0.#") + "K" : n.ToString();
            _strings[n] = s;
            return s;
        }

        private void Update()
        {
            _budget = Mathf.Min(_perSecondBudget, _budget + _perSecondBudget * Time.unscaledDeltaTime);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!_entries[i].Active) continue;
                _entries[i].Age += dt;
                float k = _entries[i].Age / _entries[i].Life;
                if (k >= 1f)
                {
                    _entries[i].Active = false;
                    _entries[i].Text.gameObject.SetActive(false);
                    continue;
                }
                var tr = _entries[i].Text.transform;
                tr.position += _entries[i].Velocity * dt;
                float pop = k < 0.2f ? Mathf.Lerp(0.6f, 1.2f, k / 0.2f) : Mathf.Lerp(1.2f, 0.9f, (k - 0.2f) / 0.8f);
                tr.localScale = new Vector3(pop, pop, pop);
                var c = _entries[i].Text.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                _entries[i].Text.color = c;
            }
        }
    }
}
