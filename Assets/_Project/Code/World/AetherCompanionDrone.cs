using System.Collections;
using UnityEngine;

// ============================================================================
//  STAGE 1: CREATE A C# SCRIPT — Assets/_Project/Code/World/AetherCompanionDrone.cs
//  STAGE 2: A CLASS AND NAMESPACE — EchoesOfTheLost.World.AetherCompanionDrone
//  STAGE 3: VARIABLES AND METHODS — Orbit parameters, state properties & methods
//  STAGE 4: APPLY SCRIPTS TO YOUR GAME — Attached to Solenne_CompanionDrone
//  STAGE 5: COROUTINES — AutonomousOrbitRoutine & ExecuteSupportScanRoutine
//  STAGE 6: USE GAME OBJECTS AS VARIABLES — _followTargetObject, _homeShrineObject, etc.
// ============================================================================
namespace EchoesOfTheLost.World
{
    /// <summary>
    /// Operational mode for the floating Solenne Aether Companion Drone.
    /// </summary>
    public enum DroneOperationMode
    {
        EscortOrbit = 0,
        ScanningTarget = 1,
        ResonanceSync = 2
    }

    /// <summary>
    /// Floating ancient Solenne automaton that orbits Kael, responds to Wayshrine
    /// attunement events via Coroutines, and manipulates child/target GameObjects
    /// assigned as Inspector variables.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class AetherCompanionDrone : MonoBehaviour
    {
        // ── STAGE 6: USE GAME OBJECTS AS VARIABLES ──────────────────────────
        [Header("Stage 6 — GameObject References (Wired in Inspector)")]
        [Tooltip("The Player (Kael) GameObject that this drone escorts.")]
        [SerializeField] private GameObject _followTargetObject;

        [Tooltip("The Solenne_AetherWayshrine GameObject this drone is linked to.")]
        [SerializeField] private GameObject _homeShrineObject;

        [Tooltip("Glowing central eye/core GameObject of the drone.")]
        [SerializeField] private GameObject _droneCoreObject;

        [Tooltip("Rotating gyroscope stabilizer ring GameObject around the drone.")]
        [SerializeField] private GameObject _stabilizerRingObject;

        [Tooltip("Holographic downward scan beam GameObject toggled during scan coroutines.")]
        [SerializeField] private GameObject _scanConeObject;

        // ── STAGE 3: VARIABLES (Serialized Primitives & Structs) ────────────
        [Header("Stage 3 — Drone Variables & Flight Parameters")]
        [SerializeField] private string _droneDesignation = "SOL-DRONE // VERDA-07";
        [SerializeField] private DroneOperationMode _operationMode = DroneOperationMode.EscortOrbit;
        [SerializeField] [Range(0.8f, 4.0f)] private float _orbitRadius = 1.65f;
        [SerializeField] [Range(1.2f, 3.5f)] private float _hoverHeight = 2.05f;
        [SerializeField] [Range(15f, 180f)] private float _orbitAngularSpeed = 52f;
        [SerializeField] [Range(1f, 15f)] private float _followSmoothSpeed = 6.5f;
        [SerializeField] [Range(0.5f, 5.0f)] private float _scanDurationSeconds = 2.4f;
        [SerializeField] private int _pulseHealAmount = 15;
        [SerializeField] private bool _isScanBeamActive;

        // ── Private Runtime Fields ──────────────────────────────────────────
        private float _currentOrbitAngleDeg = 35f;
        private Coroutine _orbitCoroutine;
        private Coroutine _activeScanCoroutine;

        // ── Public Properties ───────────────────────────────────────────────
        public string DroneDesignation => _droneDesignation;
        public DroneOperationMode OperationMode => _operationMode;
        public GameObject FollowTargetObject => _followTargetObject;
        public GameObject HomeShrineObject => _homeShrineObject;
        public GameObject ScanConeObject => _scanConeObject;
        public bool IsScanBeamActive => _isScanBeamActive;

        // ── STAGE 3: UNITY LIFECYCLE METHODS ────────────────────────────────
        private void OnEnable()
        {
            SyncVisualGameObjects();

            if (Application.isPlaying)
            {
                if (_orbitCoroutine != null) StopCoroutine(_orbitCoroutine);
                _orbitCoroutine = StartCoroutine(AutonomousOrbitRoutine());
            }
        }

        private void OnDisable()
        {
            if (_orbitCoroutine != null)
            {
                StopCoroutine(_orbitCoroutine);
                _orbitCoroutine = null;
            }
            if (_activeScanCoroutine != null)
            {
                StopCoroutine(_activeScanCoroutine);
                _activeScanCoroutine = null;
            }
        }

        private void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            UpdateStabilizerRing(dt);

            if (Application.isPlaying && _operationMode == DroneOperationMode.EscortOrbit)
            {
                UpdateEscortPosition(dt);
            }
        }

        // ── STAGE 3 & 6: CUSTOM GAMEPLAY METHODS USING GAMEOBJECTS ──────────

        /// <summary>
        /// Assigns all GameObject variable references programmatically or from the Editor builder.
        /// </summary>
        public void BindGameObjectVariables(
            GameObject followTarget,
            GameObject homeShrine,
            GameObject droneCore,
            GameObject stabilizerRing,
            GameObject scanCone)
        {
            _followTargetObject = followTarget;
            _homeShrineObject = homeShrine;
            _droneCoreObject = droneCore;
            _stabilizerRingObject = stabilizerRing;
            _scanConeObject = scanCone;
            SyncVisualGameObjects();
        }

        /// <summary>
        /// Positions the drone at a specific orbit angle around its _followTargetObject or target point.
        /// </summary>
        public void SnapToOrbitPose(float orbitAngleDegrees, bool enableScanCone, Transform customFocus = null)
        {
            _currentOrbitAngleDeg = orbitAngleDegrees;
            _isScanBeamActive = enableScanCone;
            _operationMode = enableScanCone ? DroneOperationMode.ScanningTarget : DroneOperationMode.EscortOrbit;

            Vector3 anchor = _followTargetObject != null
                ? _followTargetObject.transform.position
                : new Vector3(0f, 0.05f, 0f);

            if (customFocus != null)
            {
                anchor = customFocus.position;
            }

            float rad = _currentOrbitAngleDeg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * _orbitRadius, _hoverHeight, Mathf.Sin(rad) * _orbitRadius);
            transform.position = anchor + offset;

            Vector3 lookPoint = customFocus != null
                ? customFocus.position + Vector3.up * 1.2f
                : anchor + Vector3.up * 1.3f;
            Vector3 dir = lookPoint - transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            SyncVisualGameObjects();
        }

        private void UpdateEscortPosition(float deltaTime)
        {
            if (_followTargetObject == null) return;

            _currentOrbitAngleDeg = (_currentOrbitAngleDeg + _orbitAngularSpeed * deltaTime) % 360f;
            float rad = _currentOrbitAngleDeg * Mathf.Deg2Rad;
            float bob = Mathf.Sin(Time.time * 2.6f) * 0.14f;

            Vector3 desiredPos = _followTargetObject.transform.position + new Vector3(
                Mathf.Cos(rad) * _orbitRadius,
                _hoverHeight + bob,
                Mathf.Sin(rad) * _orbitRadius);

            transform.position = Vector3.Lerp(transform.position, desiredPos, _followSmoothSpeed * deltaTime);

            Vector3 lookDir = (_followTargetObject.transform.position + Vector3.up * 1.4f) - transform.position;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _followSmoothSpeed * deltaTime);
            }
        }

        private void UpdateStabilizerRing(float deltaTime)
        {
            if (_stabilizerRingObject != null)
            {
                float speedMultiplier = _operationMode == DroneOperationMode.ScanningTarget ? 2.4f : 1.0f;
                _stabilizerRingObject.transform.Rotate(new Vector3(0f, 145f * speedMultiplier * deltaTime, 35f * deltaTime), Space.Self);
            }

            if (_droneCoreObject != null && Application.isPlaying)
            {
                float pulse = 1.0f + Mathf.Sin(Time.time * 4.2f) * 0.08f;
                _droneCoreObject.transform.localScale = Vector3.one * (0.38f * pulse);
            }
        }

        private void SyncVisualGameObjects()
        {
            if (_scanConeObject != null && _scanConeObject.activeSelf != _isScanBeamActive)
            {
                _scanConeObject.SetActive(_isScanBeamActive);
            }
        }

        // ── STAGE 5: COROUTINES (IEnumerator Asynchronous Workflows) ────────

        /// <summary>
        /// Triggers the drone's asynchronous scan & blessing coroutine toward a target Transform.
        /// </summary>
        public void TriggerSupportScan(Transform targetTransform)
        {
            if (!gameObject.activeInHierarchy) return;

            if (Application.isPlaying)
            {
                if (_activeScanCoroutine != null) StopCoroutine(_activeScanCoroutine);
                _activeScanCoroutine = StartCoroutine(ExecuteSupportScanRoutine(targetTransform));
            }
            else
            {
                // Deterministic Edit-Mode preview of the coroutine's active scan phase
                SnapToOrbitPose(25f, enableScanCone: true, customFocus: targetTransform);
            }
        }

        /// <summary>
        /// Stage 5 Coroutine #1: Continuous background telemetry & periodic harmonic scan pulse.
        /// Uses WaitForSeconds to space out autonomous checks without bogging down Update().
        /// </summary>
        private IEnumerator AutonomousOrbitRoutine()
        {
            var waitInterval = new WaitForSeconds(6.0f);
            while (true)
            {
                yield return waitInterval;

                if (_operationMode == DroneOperationMode.EscortOrbit && _followTargetObject != null)
                {
                    // Brief 0.6s diagnostic pulse on the scan cone
                    _isScanBeamActive = true;
                    SyncVisualGameObjects();

                    if (_followTargetObject.TryGetComponent<Characters.Player.PlayerStats>(out var stats))
                    {
                        stats.Heal(_pulseHealAmount);
                    }

                    yield return new WaitForSeconds(0.6f);

                    _isScanBeamActive = false;
                    SyncVisualGameObjects();
                }
            }
        }

        /// <summary>
        /// Stage 5 Coroutine #2: Multi-step swoop, holographic scan projection, and return sequence
        /// triggered when Kael attunes the Solenne Aether Wayshrine.
        /// </summary>
        public IEnumerator ExecuteSupportScanRoutine(Transform scanTarget)
        {
            _operationMode = DroneOperationMode.ScanningTarget;
            Transform focus = scanTarget != null
                ? scanTarget
                : (_homeShrineObject != null ? _homeShrineObject.transform : transform);

            Vector3 startPos = transform.position;
            Vector3 hoverApex = focus.position + new Vector3(-1.15f, 2.75f, -1.15f);

            // Phase A: Swoop smoothly toward the Shrine / Target over 0.55 seconds
            float elapsed = 0f;
            const float approachDuration = 0.55f;
            while (elapsed < approachDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / approachDuration);
                transform.position = Vector3.Lerp(startPos, hoverApex, t);

                Vector3 lookDir = (focus.position + Vector3.up * 1.4f) - transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                yield return null;
            }

            // Phase B: Activate the _scanConeObject GameObject variable and sweep around target
            _isScanBeamActive = true;
            SyncVisualGameObjects();

            float scanElapsed = 0f;
            while (scanElapsed < _scanDurationSeconds)
            {
                scanElapsed += Time.deltaTime;
                float angle = scanElapsed * 160f * Mathf.Deg2Rad;
                Vector3 orbitOffset = new Vector3(Mathf.Cos(angle) * 1.35f, 2.65f, Mathf.Sin(angle) * 1.35f);
                transform.position = focus.position + orbitOffset;

                Vector3 lookDir = (focus.position + Vector3.up * 1.1f) - transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                yield return null;
            }

            // Phase C: Pause briefly at resonance peak, then deactivate scan beam & return to Kael
            yield return new WaitForSeconds(0.25f);

            _isScanBeamActive = false;
            _operationMode = DroneOperationMode.EscortOrbit;
            SyncVisualGameObjects();
            _activeScanCoroutine = null;
        }
    }
}
