using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

// ============================================================
//  SceneLoader — Async additive scene management
//  Attach to: _Boot scene persistent GameObject
// ============================================================
namespace EchoesOfTheLost.Core
{
    public class SceneLoader : Utils.Singleton<SceneLoader>
    {
        [Header("Loading Screen")]
        [SerializeField] private GameObject _loadingScreen;
        [SerializeField] private UnityEngine.UI.Slider _progressSlider;
        [SerializeField] private TMPro.TextMeshProUGUI _progressText;
        [SerializeField] private float _minimumLoadTime = 0.5f;

        private string _currentWorldScene;
        private bool _isLoading;

        public event Action OnLoadingStarted;
        public event Action<float> OnLoadingProgress;
        public event Action OnLoadingCompleted;

        public bool IsLoading => _isLoading;

        // ── Public API ──────────────────────────────────────

        /// <summary>Load a scene additively and optionally unload previous.</summary>
        public void LoadScene(string sceneName, string sceneToUnload = null)
        {
            if (_isLoading) return;
            StartCoroutine(LoadSceneAsync(sceneName, sceneToUnload));
        }

        /// <summary>Load the main game world.</summary>
        public void LoadWorld(string worldScene)
        {
            string prev = _currentWorldScene;
            _currentWorldScene = worldScene;
            LoadScene(worldScene, prev);
        }

        /// <summary>Return to main menu from game.</summary>
        public void LoadMainMenu()
        {
            LoadScene("MainMenu", _currentWorldScene);
            _currentWorldScene = null;
        }

        // ── Private ─────────────────────────────────────────

        private IEnumerator LoadSceneAsync(string sceneName, string sceneToUnload)
        {
            _isLoading = true;
            OnLoadingStarted?.Invoke();

            // Show loading screen
            if (_loadingScreen != null) _loadingScreen.SetActive(true);

            // Report initial progress
            SetProgress(0f);
            yield return null;

            float startTime = Time.time;

            // Unload old scene
            if (!string.IsNullOrEmpty(sceneToUnload) &&
                SceneManager.GetSceneByName(sceneToUnload).isLoaded)
            {
                var unloadOp = SceneManager.UnloadSceneAsync(sceneToUnload);
                while (!unloadOp.isDone)
                {
                    SetProgress(unloadOp.progress * 0.2f);
                    yield return null;
                }
            }

            // Load new scene
            var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            loadOp.allowSceneActivation = false;

            while (loadOp.progress < 0.9f)
            {
                SetProgress(0.2f + loadOp.progress * 0.7f);
                yield return null;
            }

            // Ensure minimum load time for UX
            float elapsed = Time.time - startTime;
            if (elapsed < _minimumLoadTime)
                yield return new WaitForSeconds(_minimumLoadTime - elapsed);

            SetProgress(1f);
            yield return new WaitForSeconds(0.1f);

            // Activate scene
            loadOp.allowSceneActivation = true;
            yield return new WaitUntil(() => loadOp.isDone);

            // Set as active scene
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

            // Hide loading screen
            if (_loadingScreen != null) _loadingScreen.SetActive(false);

            _isLoading = false;
            OnLoadingCompleted?.Invoke();
        }

        private void SetProgress(float value)
        {
            if (_progressSlider != null) _progressSlider.value = value;
            if (_progressText != null) _progressText.text = $"{Mathf.RoundToInt(value * 100)}%";
            OnLoadingProgress?.Invoke(value);
        }
    }
}
