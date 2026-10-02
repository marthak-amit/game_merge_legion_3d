using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Core
{
    public sealed class PooledMarker : MonoBehaviour
    {
        public int Key;
        public bool InPool;
    }

    /// <summary>Keyed GameObject pool. Everything that spawns repeatedly (units, projectiles, texts) goes through this.</summary>
    public static class GoPool
    {
        private static readonly Dictionary<int, Stack<GameObject>> Free = new Dictionary<int, Stack<GameObject>>();
        private static Transform _root;
        private static bool _quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Free.Clear();
            _root = null;
            _quitting = false;
            Application.quitting += () => _quitting = true;
        }

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("[Pool]");
                    go.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }
                return _root;
            }
        }

        public static GameObject Get(int key, Func<GameObject> create, Transform parent = null)
        {
            GameObject go = null;
            if (Free.TryGetValue(key, out var stack))
            {
                while (stack.Count > 0 && go == null) go = stack.Pop();
            }
            if (go == null)
            {
                go = create();
                var marker = go.GetComponent<PooledMarker>();
                if (marker == null) marker = go.AddComponent<PooledMarker>();
                marker.Key = key;
            }
            go.GetComponent<PooledMarker>().InPool = false;
            go.transform.SetParent(parent, false);
            go.SetActive(true);
            return go;
        }

        public static void Release(GameObject go)
        {
            if (go == null || _quitting) return;
            var marker = go.GetComponent<PooledMarker>();
            if (marker == null) { UnityEngine.Object.Destroy(go); return; }
            if (marker.InPool) return;
            marker.InPool = true;
            go.SetActive(false);
            go.transform.SetParent(Root, false);
            if (!Free.TryGetValue(marker.Key, out var stack)) Free[marker.Key] = stack = new Stack<GameObject>();
            stack.Push(go);
        }

        /// <summary>Drops references (scene change / tests). Pooled objects in destroyed scenes are already gone.</summary>
        public static void Clear()
        {
            Free.Clear();
        }
    }
}
