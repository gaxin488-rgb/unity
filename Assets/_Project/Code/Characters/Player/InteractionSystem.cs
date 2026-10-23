using UnityEngine;
using System.Collections;

// ============================================================
//  InteractionSystem — Detects and shows interact prompts
//  Attach to: Player GameObject
// ============================================================
namespace EchoesOfTheLost.Characters.Player
{
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float _interactRange = 2.5f;
        [SerializeField] private float _checkInterval = 0.1f;  // check 10x per second
        [SerializeField] private LayerMask _interactLayerMask;
        [SerializeField] private Transform _playerHead;

        // ── Events ────────────────────────────────────────────
        public event System.Action<string> OnTargetFound;   // prompt text
        public event System.Action OnTargetLost;

        // ── State ─────────────────────────────────────────────
        private IInteractable _currentTarget;
        private float _checkTimer;

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= _checkInterval)
            {
                _checkTimer = 0f;
                DetectInteractable();
            }
        }

        private void OnEnable()
        {
            Core.InputManager.Instance.OnInteractPressed += TryInteract;
        }

        private void OnDisable()
        {
            if (Core.InputManager.Instance != null)
                Core.InputManager.Instance.OnInteractPressed -= TryInteract;
        }

        private void DetectInteractable()
        {
            Vector3 origin = _playerHead != null ? _playerHead.position : transform.position + Vector3.up;
            var hits = Physics.OverlapSphere(origin, _interactRange, _interactLayerMask);

            IInteractable nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<IInteractable>(out var interactable)) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = interactable;
                }
            }

            if (nearest != _currentTarget)
            {
                _currentTarget = nearest;

                if (_currentTarget != null)
                    OnTargetFound?.Invoke(_currentTarget.GetInteractionPrompt());
                else
                    OnTargetLost?.Invoke();
            }
        }

        private void TryInteract()
        {
            var controller = GetComponent<PlayerController>();
            _currentTarget?.Interact(controller);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Vector3 origin = _playerHead != null ? _playerHead.position : transform.position + Vector3.up;
            Gizmos.DrawWireSphere(origin, _interactRange);
        }
    }
}
