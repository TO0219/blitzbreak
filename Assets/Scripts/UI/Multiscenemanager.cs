using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame
{
    public class MultiSceneManager : MonoBehaviour
    {
        public static MultiSceneManager Instance { get; private set; }

        // Keep this serialized in both Editor and Player so scene references survive builds.
        [SerializeField] private List<string> sceneNames = new List<string>();

        private readonly HashSet<string> _loadedScenes = new HashSet<string>();

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CacheLoadedScenes();
        }

        private void Start()
        {
            for (int i = 0; i < sceneNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(sceneNames[i]))
                    Load(sceneNames[i]);
            }
        }

        public void Load(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            StartCoroutine(LoadCoroutine(sceneName));
        }

        public void Unload(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            StartCoroutine(UnloadCoroutine(sceneName));
        }

        private IEnumerator LoadCoroutine(string sceneName)
        {
            if (_loadedScenes.Contains(sceneName))
            {
                Debug.LogWarning($"[MultiSceneManager] {sceneName} is already loaded.");
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
                yield break;

            yield return operation;
            _loadedScenes.Add(sceneName);
        }

        private IEnumerator UnloadCoroutine(string sceneName)
        {
            if (!_loadedScenes.Contains(sceneName))
            {
                Debug.LogWarning($"[MultiSceneManager] {sceneName} is not loaded.");
                yield break;
            }

            AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation == null)
                yield break;

            yield return operation;
            _loadedScenes.Remove(sceneName);
        }

        private void CacheLoadedScenes()
        {
            _loadedScenes.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    _loadedScenes.Add(scene.name);
            }
        }
    }
}
