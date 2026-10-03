using UnityEngine;

namespace BlockBloom
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (Object.FindFirstObjectByType<App>() != null) return;
            var go = new GameObject("[App]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }
    }
}
