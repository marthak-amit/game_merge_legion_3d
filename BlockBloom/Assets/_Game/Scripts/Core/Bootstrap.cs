using UnityEngine;

namespace BlockBloom
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            Debug.Log("[BlockBloom] bootstrap");
        }
    }
}
