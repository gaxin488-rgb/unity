using UnityEngine;
using System.Collections;

// ============================================================
//  CinematicSystem — Controls cutscenes via Unity Timeline
//  Attach to: World_Persistent scene or specific trigger zones
// ============================================================
namespace EchoesOfTheLost.Systems.Cinematic
{
    [RequireComponent(typeof(UnityEngine.Playables.PlayableDirector))]
    public class CinematicTrigger : MonoBehaviour
    {
        [Header("Cinematic Settings")]
        [SerializeField] private string _cinematicId;
        [SerializeField] private bool _playOnTriggerEnter = true;
        [SerializeField] private bool _playOnce = true;
        [SerializeField] private string _requiredQuestId; // optional: quest must be active

        [Header("Player Setup")]
        [SerializeField] private Transform _playerStartPosition;
        [SerializeField] private bool _teleportPlayer = false;

        private UnityEngine.Playables.PlayableDirector _director;
        private bool _hasPlayed;

        private void Awake()
        {
            _director = GetComponent<UnityEngine.Playables.PlayableDirector>();
            _director.stopped += OnCinematicStopped;
        }

        private void OnDestroy()
        {
            _director.stopped -= OnCinematicStopped;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_playOnTriggerEnter) return;
            if (!other.CompareTag("Player")) return;
            PlayCinematic();
        }

        public void PlayCinematic()
        {
            if (_hasPlayed && _playOnce) return;
            if (!string.IsNullOrEmpty(_requiredQuestId))
            {
                if (Core.ServiceLocator.TryGet<Systems.Quest.QuestManager>(out var qm))
                    if (qm.GetQuestState(_requiredQuestId) == Systems.Quest.QuestState.NotStarted) return;
            }

            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            _hasPlayed = true;

            // Disable player input
            Core.InputManager.Instance?.DisablePlayerInput();
            Core.GameManager.Instance?.SetGameState(Core.GameState.Cinematic);

            // Optionally reposition player
            if (_teleportPlayer && _playerStartPosition != null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    player.transform.SetPositionAndRotation(_playerStartPosition.position, _playerStartPosition.rotation);
            }

            // Small delay before play
            yield return new WaitForSeconds(0.1f);

            _director.Play();
        }

        private void OnCinematicStopped(UnityEngine.Playables.PlayableDirector director)
        {
            Core.InputManager.Instance?.EnablePlayerInput();
            Core.GameManager.Instance?.SetGameState(Core.GameState.Playing);

            Debug.Log($"[CinematicSystem] Finished: {_cinematicId}");
        }
    }

    // ── Camera Shake Manager ──────────────────────────────────
    public class CameraShakeManager : Utils.Singleton<CameraShakeManager>
    {
        // Cinemachine Impulse (uncomment after Cinemachine package import):
        // [Header("Cinemachine Impulse")]
        // [SerializeField] private Cinemachine.CinemachineImpulseSource _impulseSource;

        public void Shake(float intensity = 0.3f, float duration = 0.2f)
        {
            StartCoroutine(ShakeRoutine(intensity, duration));
        }

        private IEnumerator ShakeRoutine(float intensity, float duration)
        {
            // _impulseSource?.GenerateImpulse(intensity);
            yield return new WaitForSeconds(duration);
        }
    }

    // ── Status Effects ────────────────────────────────────────
    public enum StatusEffectType { Poison, Burn, Freeze, Stun, Slow, Bleed, Regenerate }

    [System.Serializable]
    public class StatusEffect
    {
        public StatusEffectType type;
        public float duration;
        public float tickDamage;     // per second (negative = heal)
        public float speedModifier;  // 1 = normal, 0.5 = 50% slower
        public bool isStunned;
        public bool isFrozen;
        [System.NonSerialized] public float timeRemaining;
    }

    public class StatusEffectSystem : MonoBehaviour
    {
        private System.Collections.Generic.List<StatusEffect> _activeEffects
            = new System.Collections.Generic.List<StatusEffect>();

        private Characters.Player.PlayerStats _stats;

        public event System.Action<StatusEffect> OnEffectApplied;
        public event System.Action<StatusEffectType> OnEffectExpired;

        private void Awake() => _stats = GetComponent<Characters.Player.PlayerStats>();

        private void Update()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                effect.timeRemaining -= Time.deltaTime;

                // Apply tick damage/heal
                if (effect.tickDamage != 0f)
                {
                    if (effect.tickDamage > 0f)
                        _stats?.TakeDamage(effect.tickDamage * Time.deltaTime, ignoreDefense: true);
                    else
                        _stats?.Heal(-effect.tickDamage * Time.deltaTime);
                }

                if (effect.timeRemaining <= 0f)
                {
                    OnEffectExpired?.Invoke(effect.type);
                    _activeEffects.RemoveAt(i);
                }
            }
        }

        public void ApplyEffect(StatusEffect effect)
        {
            // Refresh if same type already active
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].type == effect.type)
                {
                    _activeEffects[i].timeRemaining = Mathf.Max(_activeEffects[i].timeRemaining, effect.duration);
                    return;
                }
            }

            effect.timeRemaining = effect.duration;
            _activeEffects.Add(effect);
            OnEffectApplied?.Invoke(effect);
        }

        public void ClearEffect(StatusEffectType type)
        {
            _activeEffects.RemoveAll(e => e.type == type);
        }

        public bool HasEffect(StatusEffectType type) =>
            _activeEffects.Exists(e => e.type == type);

        public float GetSpeedModifier()
        {
            float mod = 1f;
            foreach (var e in _activeEffects) mod *= e.speedModifier;
            return mod;
        }

        public bool IsStunned() => _activeEffects.Exists(e => e.isStunned && e.timeRemaining > 0f);
        public bool IsFrozen() => _activeEffects.Exists(e => e.isFrozen && e.timeRemaining > 0f);
    }
}
