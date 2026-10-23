using UnityEngine;

// ============================================================================
//  PIPELINE B — STAGE 3: USE TRIGGERS IN YOUR GAME
//  Assets/_Project/Code/World/GameplayTriggerVolume.cs
//
//  Demonstrates Unity Physics Trigger Volumes (Collider.isTrigger = true)
//  responding to OnTriggerEnter, OnTriggerStay, and OnTriggerExit events:
//    1. SpeedBoostGate — Boosts player movement & triggers Shrine resonance
//    2. HealingSanctuary — Regenerates HP & Stamina continuously while inside
//    3. RuinsAmbushAlarm — Alerts courtyard guardians & shifts beacon lighting
// ============================================================================
namespace EchoesOfTheLost.World
{
    public enum TriggerVolumeType
    {
        SpeedBoostGate = 0,
        HealingSanctuary = 1,
        RuinsAmbushAlarm = 2
    }

    public struct GameplayTriggerActivatedEvent : Core.IGameEvent
    {
        public string TriggerId;
        public TriggerVolumeType VolumeType;
        public bool IsEntered;
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class GameplayTriggerVolume : MonoBehaviour
    {
        [Header("Trigger Identity & Configuration")]
        [SerializeField] private string _triggerId = "trigger_aether_gate";
        [SerializeField] private string _triggerDisplayName = "Aether Speed Gate";
        [SerializeField] private TriggerVolumeType _volumeType = TriggerVolumeType.SpeedBoostGate;
        [SerializeField] private string _activatorTag = "Player";

        [Header("Gameplay Parameters")]
        [SerializeField] private float _healPerSecond = 10f;
        [SerializeField] private float _staminaPerSecond = 18f;
        [SerializeField] private int _discoveryXpBonus = 25;

        [Header("Linked Scene GameObjects (Stage 6 Integration)")]
        [Tooltip("Visual glowing boundary ring GameObject indicating the trigger zone.")]
        [SerializeField] private GameObject _visualBoundaryRingObject;

        [Tooltip("Optional linked SolenneAetherShrine or beacon GameObject notified on trigger enter.")]
        [SerializeField] private GameObject _linkedShrineOrBeaconObject;

        [SerializeField] private Light _zoneLight;

        private Collider _triggerCollider;
        private bool _isPlayerInside;
        private bool _hasGrantedBonusXp;
        private float _healAccumulator;

        public string TriggerId => _triggerId;
        public string TriggerDisplayName => _triggerDisplayName;
        public TriggerVolumeType VolumeType => _volumeType;
        public bool IsPlayerInside => _isPlayerInside;
        public Collider TriggerCollider => _triggerCollider != null ? _triggerCollider : (_triggerCollider = GetComponent<Collider>());

        private void Awake()
        {
            EnsureColliderIsTrigger();
        }

        private void OnEnable()
        {
            EnsureColliderIsTrigger();
        }

        public void ConfigureTrigger(
            string id,
            string displayName,
            TriggerVolumeType volumeType,
            GameObject boundaryRing,
            GameObject linkedObject,
            Light zoneLight)
        {
            _triggerId = id;
            _triggerDisplayName = displayName;
            _volumeType = volumeType;
            _visualBoundaryRingObject = boundaryRing;
            _linkedShrineOrBeaconObject = linkedObject;
            _zoneLight = zoneLight;
            EnsureColliderIsTrigger();
        }

        private void EnsureColliderIsTrigger()
        {
            if (_triggerCollider == null)
                _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
        }

        private void Update()
        {
            if (_visualBoundaryRingObject != null)
            {
                float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
                float speed = _isPlayerInside ? 95f : 32f;
                _visualBoundaryRingObject.transform.Rotate(Vector3.up, speed * dt, Space.World);
            }
        }

        // ── UNITY PHYSICS TRIGGER CALLBACKS ─────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActivator(other)) return;
            HandleTriggerEnter(other.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsActivator(other)) return;

            if (_volumeType == TriggerVolumeType.HealingSanctuary &&
                other.TryGetComponent<Characters.Player.PlayerStats>(out var stats))
            {
                _healAccumulator += _healPerSecond * Time.deltaTime;
                if (_healAccumulator >= 1f)
                {
                    int wholeHeal = Mathf.FloorToInt(_healAccumulator);
                    _healAccumulator -= wholeHeal;
                    stats.Heal(wholeHeal);
                }
                stats.RestoreStamina(_staminaPerSecond * Time.deltaTime);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsActivator(other)) return;
            HandleTriggerExit(other.gameObject);
        }

        /// <summary>
        /// Executes the trigger's enter logic (called by OnTriggerEnter in Play Mode or by verification tools).
        /// </summary>
        public void HandleTriggerEnter(GameObject activator)
        {
            _isPlayerInside = true;

            if (_zoneLight != null)
                _zoneLight.intensity = 3.6f;

            if (!_hasGrantedBonusXp && activator != null &&
                activator.TryGetComponent<Characters.Player.PlayerStats>(out var stats))
            {
                _hasGrantedBonusXp = true;
                stats.AddXP(_discoveryXpBonus);
                if (_volumeType == TriggerVolumeType.SpeedBoostGate)
                {
                    stats.RestoreStamina(100f);
                }
            }

            if (_volumeType == TriggerVolumeType.SpeedBoostGate && _linkedShrineOrBeaconObject != null &&
                _linkedShrineOrBeaconObject.TryGetComponent<SolenneAetherShrine>(out var shrine) &&
                !shrine.IsAttuned)
            {
                var pc = activator != null ? activator.GetComponent<Characters.Player.PlayerController>() : null;
                shrine.TriggerAttunementSequence(pc);
            }

            Core.EventBus.Publish(new GameplayTriggerActivatedEvent
            {
                TriggerId = _triggerId,
                VolumeType = _volumeType,
                IsEntered = true
            });
        }

        public void HandleTriggerExit(GameObject activator)
        {
            _isPlayerInside = false;
            if (_zoneLight != null)
                _zoneLight.intensity = 1.8f;

            Core.EventBus.Publish(new GameplayTriggerActivatedEvent
            {
                TriggerId = _triggerId,
                VolumeType = _volumeType,
                IsEntered = false
            });
        }

        private bool IsActivator(Collider other)
        {
            if (other == null) return false;
            if (!string.IsNullOrEmpty(_activatorTag) && other.CompareTag(_activatorTag))
                return true;
            return other.GetComponent<Characters.Player.PlayerController>() != null;
        }
    }
}
