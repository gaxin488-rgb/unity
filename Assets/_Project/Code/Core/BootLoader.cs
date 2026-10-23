using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

// ============================================================
//  BootLoader — Entry point of the game
//  Attach to: GameObject in _Boot scene
//  Purpose: Initialize all persistent systems, then load MainMenu
// ============================================================
namespace EchoesOfTheLost.Core
{
    public class BootLoader : MonoBehaviour
    {
        [Header("Boot Screen")]
        [SerializeField] private CanvasGroup _splashScreen;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Settings")]
        [SerializeField] private float _minimumSplashDuration = 2f;
        [SerializeField] private string _mainMenuScene = "MainMenu";

        private void Start()
        {
            StartCoroutine(BootSequence());
        }

        private IEnumerator BootSequence()
        {
            float startTime = Time.time;

            // Step 1: Initialize Application settings
            SetStatus("Initializing...", 0f);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            yield return null;

            // Step 2: GameManager (auto-initializes via Singleton)
            SetStatus("Loading systems...", 0.2f);
            yield return null;

            // Step 3: Load saved settings (audio, graphics, controls)
            SetStatus("Loading settings...", 0.4f);
            // SaveSystem.Instance.LoadSettings();
            yield return null;

            // Step 4: Warm up object pools
            SetStatus("Preparing world...", 0.6f);
            yield return null;

            // Step 5: Preload addressables catalog
            SetStatus("Loading assets...", 0.8f);
            yield return null;

            SetStatus("Ready!", 1f);

            // Ensure minimum splash duration
            float elapsed = Time.time - startTime;
            if (elapsed < _minimumSplashDuration)
                yield return new WaitForSeconds(_minimumSplashDuration - elapsed);

            // Fade out splash
            yield return FadeOut(_splashScreen, 0.5f);

            // Load main menu
            SceneManager.LoadScene(_mainMenuScene, LoadSceneMode.Additive);
        }

        private void SetStatus(string message, float progress)
        {
            if (_statusText != null) _statusText.text = message;
            if (_progressBar != null) _progressBar.fillAmount = progress;
        }

        private IEnumerator FadeOut(CanvasGroup cg, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cg.alpha = 1f - (elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            cg.alpha = 0f;
            cg.gameObject.SetActive(false);
        }
    }
}
