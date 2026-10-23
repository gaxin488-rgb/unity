using System.Collections;
using UnityEngine;

// ============================================================================
//  STAGE 1: CREATE A C# SCRIPT — Assets/_Project/Code/World/SolenneAetherShrine.cs
//  STAGE 2: A CLASS AND NAMESPACE — EchoesOfTheLost.World.SolenneAetherShrine
//  STAGE 3: VARIABLES AND METHODS — Serialized fields, properties, lifecycle & custom methods
//  STAGE 4: APPLY SCRIPTS TO YOUR GAME — Attached to Solenne_AetherWayshrine in Sandbox.unity
//  STAGE 5: COROUTINES — AmbientResonancePulseRoutine & AttunementSequenceRoutine
//  STAGE 6: USE GAME OBJECTS AS VARIABLES — 8 scene GameObjects wired in the Inspector
// ============================================================================
namespace EchoesOfTheLost.World
{
    /// <summary>
    /// Enumeration of resonance states for a Solenne Aether Wayshrine.
    /// </summary>
    public enum ShrineResonanceState
    {
        Dormant = 0,
        ChargingSurge = 1,
        Attuned = 2
    }

    /// <summary>
    /// Serializable struct holding reward & buff parameters granted when a shrine is attuned.
    /// </summary>
    [System.Serializable]
    public struct ShrineBlessingData
    {
        public int ExperienceReward;
        public int GoldBlessing;
        public int HealAmount;
        public float StaminaRestoreAmount;
    }

    /// <summary>
    /// Event published onto Core.EventBus when any SolenneAetherShrine completes attunement.
    /// </summary>
    public struct ShrineAttunedEvent : Core.IGameEvent
    {
        public string ShrineId;
        public string ShrineTitle;
        public Vector3 WorldPosition;
    }

    /// <summary>
    /// Interactive Solenne Aether Wayshrine demonstrating all 6 Unity C# Scripting stages:
    /// class & namespace architecture, serialized variables & methods, scene attachment,
    /// multi-phase Coroutines, and direct GameObject variable manipulation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(Collider))]
    public class SolenneAetherShrine : MonoBehaviour, Characters.Player.IInteractable
    {
        // ── STAGE 6: USE GAME OBJECTS AS VARIABLES ──────────────────────────
        [Header("Stage 6 — GameObject Variables (Wired Scene References)")]
        [Tooltip("Levitating central Aether sphere GameObject inside the shrine spire.")]
        [SerializeField] private GameObject _floatingCoreObject;

        [Tooltip("Inner tilted gyroscope ring GameObject rotating around the core.")]
        [SerializeField] private GameObject _innerRingObject;

        [Tooltip("Outer runic halo ring GameObject counter-rotating around the spire.")]
        [SerializeField] private GameObject _outerRingObject;

        [Tooltip("High-energy Aether conduit beam GameObject connecting to the Sanctum Pylon.")]
        [SerializeField] private GameObject _energyBeamObject;

        [Tooltip("Expanding ground shockwave ring GameObject pulsed during attunement.")]
        [SerializeField] private GameObject _shockwaveRingObject;

        [Tooltip("Distant Solenne_SanctumPylon GameObject in the Watchtower courtyard.")]
        [SerializeField] private GameObject _linkedSanctumPylonObject;

        [Tooltip("Floating Solenne_CompanionDrone GameObject that responds to shrine attunement.")]
        [SerializeField] private GameObject _companionDroneObject;

        [Tooltip("Reference to the Player (Kael) GameObject in the scene.")]
        [SerializeField] private GameObject _playerTargetObject;

        // ── STAGE 3: VARIABLES (Inspector Serialized Fields & Attributes) ────
        [Header("Stage 3 — Shrine Identity & State Variables")]
        [SerializeField] private string _shrineId = "shrine_verda_bridge";
        [SerializeField] private string _shrineTitle = "Solenne Aether Wayshrine";
        [SerializeField] private ShrineResonanceState _currentState = ShrineResonanceState.Dormant;
        [SerializeField] private bool _isAttuned;

        [Header("Stage 3 — Orbital & Levitation Mechanics")]
        [SerializeField] [Range(0.5f, 4.0f)] private float _coreBaseHeight = 2.05f;
        [SerializeField] [Range(0.02f, 0.6f)] private float _levitationAmplitude = 0.18f;
        [SerializeField] [Range(0.5f, 6.0f)] private float _levitationFrequency = 2.2f;
        [SerializeField] [Range(10f, 240f)] private float _innerRingSpinSpeed = 68f;
        [SerializeField] [Range(10f, 240f)] private float _outerRingSpinSpeed = 42f;
        [SerializeField] [Range(1.0f, 5.0f)] private float _surgeSpeedMultiplier = 3.2f;

        [Header("Stage 3 — Visual Colors & Lighting")]
        [SerializeField] private Color _dormantColor = new Color(0.18f, 0.72f, 0.95f, 1f);
        [SerializeField] private Color _surgeColor = new Color(1.00f, 0.84f, 0.28f, 1f);
        [SerializeField] private Color _attunedColor = new Color(0.22f, 0.98f, 0.88f, 1f);
        [SerializeField] private Light _shrinePointLight;

        [Header("Stage 3 — Blessing Rewards Struct")]
        [SerializeField] private ShrineBlessingData _blessingData = new ShrineBlessingData
        {
            ExperienceReward = 75,
            GoldBlessing = 50,
            HealAmount = 40,
            StaminaRestoreAmount = 50f
        };

        // ── Private Runtime State ───────────────────────────────────────────
        private Coroutine _ambientPulseCoroutine;
        private Coroutine _attunementSequenceCoroutine;
        private float _currentSpinMultiplier = 1.0f;

        // ── Public Read-Only Properties ─────────────────────────────────────
        public string ShrineId => _shrineId;
        public string ShrineTitle => _shrineTitle;
        public ShrineResonanceState CurrentState => _currentState;
        public bool IsAttuned => _isAttuned;
        public GameObject FloatingCoreObject => _floatingCoreObject;
        public GameObject InnerRingObject => _innerRingObject;
        public GameObject OuterRingObject => _outerRingObject;
        public GameObject EnergyBeamObject => _energyBeamObject;
        public GameObject LinkedSanctumPylonObject => _linkedSanctumPylonObject;
        public GameObject CompanionDroneObject => _companionDroneObject;
        public GameObject PlayerTargetObject => _playerTargetObject;

        // ── STAGE 3: UNITY LIFECYCLE METHODS ────────────────────────────────
        private void Awake()
        {
            if (_shrinePointLight == null)
                _shrinePointLight = GetComponentInChildren<Light>();
        }

        private void OnEnable()
        {
            ApplyVisualStateImmediate(_currentState);

            if (Application.isPlaying)
            {
                if (_ambientPulseCoroutine != null) StopCoroutine(_ambientPulseCoroutine);
                _ambientPulseCoroutine = StartCoroutine(AmbientResonancePulseRoutine());
            }
        }

        private void OnDisable()
        {
            if (_ambientPulseCoroutine != null)
            {
                StopCoroutine(_ambientPulseCoroutine);
                _ambientPulseCoroutine = null;
            }
            if (_attunementSequenceCoroutine != null)
            {
                StopCoroutine(_attunementSequenceCoroutine);
                _attunementSequenceCoroutine = null;
            }
        }

        private void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            float timeNow = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
            UpdateOrbitalMechanics(dt, timeNow);
        }

        // ── STAGE 3 & 6: CUSTOM METHODS OPERATING ON GAMEOBJECT VARIABLES ────

        /// <summary>
        /// Wires all 8 GameObject references into serialized fields on this component.
        /// </summary>
        public void BindGameObjectVariables(
            GameObject floatingCore,
            GameObject innerRing,
            GameObject outerRing,
            GameObject energyBeam,
            GameObject shockwaveRing,
            GameObject linkedSanctumPylon,
            GameObject companionDrone,
            GameObject playerTarget,
            Light pointLight)
        {
            _floatingCoreObject = floatingCore;
            _innerRingObject = innerRing;
            _outerRingObject = outerRing;
            _energyBeamObject = energyBeam;
            _shockwaveRingObject = shockwaveRing;
            _linkedSanctumPylonObject = linkedSanctumPylon;
            _companionDroneObject = companionDrone;
            _playerTargetObject = playerTarget;
            _shrinePointLight = pointLight;

            OrientEnergyBeamToTarget();
            ApplyVisualStateImmediate(_currentState);
        }

        /// <summary>
        /// Rotates the inner/outer rings and levitates the floating core GameObject.
        /// </summary>
        public void UpdateOrbitalMechanics(float deltaTime, float timeSeconds)
        {
            if (_floatingCoreObject != null)
            {
                float heightBoost = _currentState == ShrineResonanceState.ChargingSurge ? 0.38f :
                                    (_currentState == ShrineResonanceState.Attuned ? 0.24f : 0f);
                float bob = Mathf.Sin(timeSeconds * _levitationFrequency) * _levitationAmplitude;
                _floatingCoreObject.transform.localPosition = new Vector3(0f, _coreBaseHeight + heightBoost + bob, 0f);
                _floatingCoreObject.transform.Rotate(Vector3.up, 45f * _currentSpinMultiplier * deltaTime, Space.Self);
            }

            if (_innerRingObject != null)
            {
                _innerRingObject.transform.Rotate(
                    new Vector3(1.0f, 1.35f, 0.35f).normalized,
                    _innerRingSpinSpeed * _currentSpinMultiplier * deltaTime,
                    Space.Self);
            }

            if (_outerRingObject != null)
            {
                _outerRingObject.transform.Rotate(
                    new Vector3(-0.45f, 1.0f, 0.8f).normalized,
                    _outerRingSpinSpeed * _currentSpinMultiplier * deltaTime,
                    Space.Self);
            }
        }

        /// <summary>
        /// Aligns the _energyBeamObject cylinder between the floating core and _linkedSanctumPylonObject.
        /// </summary>
        public void OrientEnergyBeamToTarget()
        {
            if (_energyBeamObject == null || _linkedSanctumPylonObject == null) return;

            Vector3 startWorld = _floatingCoreObject != null
                ? _floatingCoreObject.transform.position
                : transform.position + Vector3.up * _coreBaseHeight;
            Vector3 endWorld = _linkedSanctumPylonObject.transform.position + Vector3.up * 2.2f;

            Vector3 delta = endWorld - startWorld;
            float distance = delta.magnitude;
            if (distance < 0.01f) return;

            _energyBeamObject.transform.position = startWorld + delta * 0.5f;
            _energyBeamObject.transform.up = delta.normalized;
            _energyBeamObject.transform.localScale = new Vector3(0.16f, distance * 0.5f, 0.16f);
        }

        /// <summary>
        /// IInteractable implementation called when Kael presses [E] near the Wayshrine.
        /// </summary>
        public void Interact(Characters.Player.PlayerController player)
        {
            TriggerAttunementSequence(player);
        }

        /// <summary>
        /// Returns context-sensitive HUD interaction prompt.
        /// </summary>
        public string GetInteractionPrompt()
        {
            return _isAttuned
                ? $"[E] Channel {_shrineTitle} (Pulse Resonance)"
                : $"[E] Attune {_shrineTitle} (+{_blessingData.ExperienceReward} XP)";
        }

        /// <summary>
        /// Applies healing, stamina restoration, XP, and Gold to the interacting player.
        /// </summary>
        public void ApplyAetherBlessing(Characters.Player.PlayerController player)
        {
            GameObject targetGO = player != null ? player.gameObject : _playerTargetObject;
            if (targetGO != null && targetGO.TryGetComponent<Characters.Player.PlayerStats>(out var stats))
            {
                stats.Heal(_blessingData.HealAmount);
                stats.RestoreStamina(_blessingData.StaminaRestoreAmount);
                stats.AddXP(_blessingData.ExperienceReward);
            }

            if (Systems.Economy.EconomySystem.Instance != null)
            {
                Systems.Economy.EconomySystem.Instance.AddGold(_blessingData.GoldBlessing);
            }
        }

        // ── STAGE 5: COROUTINES (Asynchronous Multi-Phase Sequences) ────────

        /// <summary>
        /// Starts the multi-phase Attunement Coroutine (or evaluates it deterministically in Edit Mode).
        /// </summary>
        public void TriggerAttunementSequence(Characters.Player.PlayerController player = null)
        {
            if (Application.isPlaying)
            {
                if (_attunementSequenceCoroutine != null)
                    StopCoroutine(_attunementSequenceCoroutine);
                _attunementSequenceCoroutine = StartCoroutine(AttunementSequenceRoutine(player));
            }
            else
            {
                EvaluateCoroutineState(ShrineResonanceState.Attuned, 1.0f);
            }
        }

        /// <summary>
        /// Stage 5 Coroutine #1: Continuous ambient harmonic pulse loop using WaitForSeconds
        /// and smooth per-frame interpolation.
        /// </summary>
        private IEnumerator AmbientResonancePulseRoutine()
        {
            var pauseBetweenPulses = new WaitForSeconds(1.25f);
            while (true)
            {
                if (_currentState != ShrineResonanceState.ChargingSurge && _shrinePointLight != null)
                {
                    float baseIntensity = _isAttuned ? 3.8f : 2.2f;
                    float duration = 1.6f;
                    float elapsed = 0f;

                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float wave = Mathf.Sin((elapsed / duration) * Mathf.PI);
                        _shrinePointLight.intensity = baseIntensity + wave * 1.15f;
                        yield return null;
                    }
                }

                yield return pauseBetweenPulses;
            }
        }

        /// <summary>
        /// Stage 5 Coroutine #2: Multi-stage cinematic attunement sequence:
        ///   Phase 1 (1.0s): High-speed ring spin-up & floating core ascension
        ///   Phase 2 (0.8s): Conduit energy beam ignition & expanding ground shockwave ring
        ///   Phase 3: Grant Aether Blessing, command Companion Drone scan coroutine, and lock Attuned state
        /// </summary>
        public IEnumerator AttunementSequenceRoutine(Characters.Player.PlayerController player)
        {
            _currentState = ShrineResonanceState.ChargingSurge;
            _currentSpinMultiplier = _surgeSpeedMultiplier;

            if (_shockwaveRingObject != null)
            {
                _shockwaveRingObject.SetActive(true);
                _shockwaveRingObject.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
            }

            // Phase 1: Spin-up & Shockwave expansion over 1.0 second
            const float surgeDuration = 1.0f;
            float elapsed = 0f;
            while (elapsed < surgeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / surgeDuration);
                EvaluateCoroutineState(ShrineResonanceState.ChargingSurge, t);
                yield return null;
            }

            // Phase 2: Activate Conduit Energy Beam linking to Solenne_SanctumPylon
            OrientEnergyBeamToTarget();
            if (_energyBeamObject != null) _energyBeamObject.SetActive(true);

            // Command the Companion Drone GameObject variable to swoop & scan
            if (_companionDroneObject != null &&
                _companionDroneObject.TryGetComponent<AetherCompanionDrone>(out var drone))
            {
                drone.TriggerSupportScan(transform);
            }

            yield return new WaitForSeconds(0.75f);

            // Phase 3: Complete Attunement, apply player rewards & publish EventBus notification
            bool firstTime = !_isAttuned;
            _isAttuned = true;
            _currentState = ShrineResonanceState.Attuned;
            _currentSpinMultiplier = 1.65f;
            EvaluateCoroutineState(ShrineResonanceState.Attuned, 1.0f);

            if (firstTime)
            {
                ApplyAetherBlessing(player);
            }

            Core.EventBus.Publish(new ShrineAttunedEvent
            {
                ShrineId = _shrineId,
                ShrineTitle = _shrineTitle,
                WorldPosition = transform.position
            });

            Core.EventBus.Publish(new Core.ItemPickedUpEvent
            {
                ItemId = _shrineId,
                Quantity = 1
            });

            _attunementSequenceCoroutine = null;
        }

        /// <summary>
        /// Deterministically evaluates the shrine's child GameObjects and lights at any coroutine phase.
        /// Works in both Play Mode and Edit Mode for instant visual verification.
        /// </summary>
        public void EvaluateCoroutineState(ShrineResonanceState state, float normalizedProgress)
        {
            _currentState = state;
            _isAttuned = state == ShrineResonanceState.Attuned;
            normalizedProgress = Mathf.Clamp01(normalizedProgress);

            _currentSpinMultiplier = state == ShrineResonanceState.ChargingSurge
                ? Mathf.Lerp(1.0f, _surgeSpeedMultiplier, normalizedProgress)
                : (state == ShrineResonanceState.Attuned ? 1.65f : 1.0f);

            UpdateOrbitalMechanics(0.08f, normalizedProgress * 2.5f);
            OrientEnergyBeamToTarget();

            if (_shockwaveRingObject != null)
            {
                bool showWave = state == ShrineResonanceState.ChargingSurge || state == ShrineResonanceState.Attuned;
                _shockwaveRingObject.SetActive(showWave);
                float radius = state == ShrineResonanceState.ChargingSurge
                    ? Mathf.Lerp(1.4f, 4.6f, normalizedProgress)
                    : 3.4f;
                _shockwaveRingObject.transform.localScale = new Vector3(radius, 0.04f, radius);
            }

            if (_energyBeamObject != null)
            {
                bool showBeam = (state == ShrineResonanceState.ChargingSurge && normalizedProgress >= 0.45f)
                                || state == ShrineResonanceState.Attuned;
                _energyBeamObject.SetActive(showBeam);
            }

            if (_shrinePointLight != null)
            {
                _shrinePointLight.color = state == ShrineResonanceState.ChargingSurge
                    ? Color.Lerp(_dormantColor, _surgeColor, normalizedProgress)
                    : (state == ShrineResonanceState.Attuned ? _attunedColor : _dormantColor);
                _shrinePointLight.intensity = state == ShrineResonanceState.ChargingSurge
                    ? Mathf.Lerp(2.4f, 5.2f, normalizedProgress)
                    : (state == ShrineResonanceState.Attuned ? 4.0f : 2.2f);
                _shrinePointLight.range = state == ShrineResonanceState.Attuned ? 14f : 10f;
            }
        }

        private void ApplyVisualStateImmediate(ShrineResonanceState state)
        {
            EvaluateCoroutineState(state, state == ShrineResonanceState.Dormant ? 0f : 1f);
        }
    }
}
