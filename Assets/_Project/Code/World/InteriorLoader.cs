using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// ============================================================
//  InteriorLoader — Loads interior scenes additively on entry
//  Attach to: Door/Entrance triggers
// ============================================================
namespace EchoesOfTheLost.World
{
    [RequireComponent(typeof(Collider))]
    public class InteriorLoader : MonoBehaviour, Characters.Player.IInteractable
    {
        [Header("Interior Settings")]
        [SerializeField] private string _interiorSceneName;
        [SerializeField] private string _interiorId;    // unique per building
        [SerializeField] private Transform _interiorSpawnPoint;

        [Header("Exterior Reference")]
        [SerializeField] private Transform _exteriorReturnPoint;

        [Header("Transition")]
        [SerializeField] private float _fadeTime = 0.5f;
        [SerializeField] private bool _triggerOnEnter = false;
        [SerializeField] private bool _requireInteract = true;

        private bool _isLoading;
        private bool _interiorLoaded;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggerOnEnter && other.CompareTag("Player"))
                EnterInterior();
        }

        public void Interact(Characters.Player.PlayerController player)
        {
            if (_requireInteract) EnterInterior();
        }

        public string GetInteractionPrompt() => "Enter";

        // ── Enter Interior ────────────────────────────────────

        private void EnterInterior()
        {
            if (_isLoading) return;
            StartCoroutine(LoadInteriorRoutine());
        }

        private IEnumerator LoadInteriorRoutine()
        {
            _isLoading = true;

            // Fade out
            yield return FadeScreen(0f, 1f, _fadeTime);

            // Disable exterior rendering (hide terrain, foliage etc.)
            if (Core.ServiceLocator.TryGet<FoliageSystem>(out var foliage))
                foliage.gameObject.SetActive(false);

            // Move player to interior spawn
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && _interiorSpawnPoint != null)
                player.transform.SetPositionAndRotation(
                    _interiorSpawnPoint.position,
                    _interiorSpawnPoint.rotation);

            // Load interior scene additively
            if (!string.IsNullOrEmpty(_interiorSceneName) && !_interiorLoaded)
            {
                var op = SceneManager.LoadSceneAsync(_interiorSceneName, LoadSceneMode.Additive);
                yield return op;
                _interiorLoaded = true;
            }

            // Fade in
            yield return FadeScreen(1f, 0f, _fadeTime);
            _isLoading = false;
        }

        public void ExitInterior()
        {
            StartCoroutine(ExitInteriorRoutine());
        }

        private IEnumerator ExitInteriorRoutine()
        {
            _isLoading = true;

            yield return FadeScreen(0f, 1f, _fadeTime);

            // Move player to exterior return point
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && _exteriorReturnPoint != null)
                player.transform.SetPositionAndRotation(
                    _exteriorReturnPoint.position,
                    _exteriorReturnPoint.rotation);

            // Unload interior
            if (_interiorLoaded && !string.IsNullOrEmpty(_interiorSceneName))
            {
                var op = SceneManager.UnloadSceneAsync(_interiorSceneName);
                yield return op;
                _interiorLoaded = false;
            }

            // Restore exterior
            if (Core.ServiceLocator.TryGet<FoliageSystem>(out var foliage))
                foliage.gameObject.SetActive(true);

            yield return FadeScreen(1f, 0f, _fadeTime);
            _isLoading = false;
        }

        private IEnumerator FadeScreen(float from, float to, float duration)
        {
            // TODO: Connect to UIManager fade overlay
            yield return new WaitForSeconds(duration);
        }

        private void OnDrawGizmosSelected()
        {
            if (_interiorSpawnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_interiorSpawnPoint.position, 0.5f);
                Gizmos.DrawLine(transform.position, _interiorSpawnPoint.position);
            }
            if (_exteriorReturnPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_exteriorReturnPoint.position, 0.5f);
            }
        }
    }

    // ── Exit Door ─────────────────────────────────────────────
    [RequireComponent(typeof(Collider))]
    public class InteriorExit : MonoBehaviour, Characters.Player.IInteractable
    {
        [SerializeField] private InteriorLoader _loader;

        private void Awake() => GetComponent<Collider>().isTrigger = true;

        public void Interact(Characters.Player.PlayerController player) => _loader?.ExitInterior();
        public string GetInteractionPrompt() => "Exit";
    }
}
