using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeLegion.Core
{
    public sealed class SceneLoader : MonoBehaviour
    {
        public bool IsLoading { get; private set; }

        public void Load(string sceneName, Action onLoaded = null)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName, onLoaded));
        }

        private IEnumerator LoadRoutine(string sceneName, Action onLoaded)
        {
            IsLoading = true;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            IsLoading = false;
            onLoaded?.Invoke();
        }
    }
}
