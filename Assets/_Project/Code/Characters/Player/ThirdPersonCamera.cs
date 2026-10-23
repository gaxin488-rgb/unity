using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace EchoesOfTheLost.Characters.Player
{
    public enum CameraViewPreset
    {
        Exploration = 0,
        CloseAction = 1,
        PanoramicTactical = 2
    }

    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target & Framing")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _pivotOffset = new Vector3(0.35f, 1.75f, 0f);
        [SerializeField] private CameraViewPreset _currentPreset = CameraViewPreset.Exploration;

        [Header("Orbit & Zoom")]
        [SerializeField] private float _distance = 5.8f;
        [SerializeField] private float _minDistance = 2.0f;
        [SerializeField] private float _maxDistance = 12.0f;
        [SerializeField] private float _zoomSpeed = 0.85f;
        [SerializeField] private float _minPitch = -25f;
        [SerializeField] private float _maxPitch = 72f;
        [SerializeField] private float _sensitivity = 0.16f;
        [SerializeField] private float _rotationSmoothTime = 0.035f;
        [SerializeField] private float _followSmoothing = 18f;

        [Header("Field of View")]
        [SerializeField] private float _targetFOV = 58f;
        [SerializeField] private float _fovSmoothing = 8f;

        [Header("Collision Avoidance")]
        [SerializeField] private float _collisionRadius = 0.25f;
        [SerializeField] private float _minCollisionDistance = 0.8f;
        [SerializeField] private LayerMask _collisionMask = ~0;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursor = true;

        private float _targetYaw = 14f;
        private float _targetPitch = 14f;
        private float _yaw = 14f;
        private float _pitch = 14f;
        private float _yawVel;
        private float _pitchVel;

        private float _currentDistance = 5.8f;
        private Vector3 _smoothedPivot;
        private Camera _cam;

        public float CurrentYaw => _yaw;
        public float CurrentPitch => _pitch;
        public CameraViewPreset CurrentPreset => _currentPreset;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _currentDistance = _distance;
        }

        private void Start()
        {
            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _target = player.transform;
            }

            ApplyPresetValues(_currentPreset, instant: true);

            if (_target != null)
            {
                _smoothedPivot = GetWorldPivot();
            }

            if (_lockCursor)
            {
                LockCursorForGameplay();
            }
        }

        public void SetAngles(float yaw, float pitch)
        {
            _targetYaw = _yaw = yaw;
            _targetPitch = _pitch = Mathf.Clamp(pitch, _minPitch, _maxPitch);
            _yawVel = 0f;
            _pitchVel = 0f;
        }

        private bool IsModalWindowBlocking()
        {
            return Core.ServiceLocator.TryGet<UI.UIManager>(out var ui) && ui != null && ui.IsWindowOpen;
        }

        private void LockCursorForGameplay()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            bool uiOpen = IsModalWindowBlocking();

            // Re-lock cursor when clicking inside the game view during active gameplay
            if (!uiOpen && _lockCursor && Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
                {
                    if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                    {
                        LockCursorForGameplay();
                    }
                }
            }

            if (!uiOpen)
            {
                HandlePresetAndZoomInput();

                // Allow smooth orbit when cursor is locked OR while holding Right Mouse Button
                bool canLook = Cursor.lockState == CursorLockMode.Locked ||
                               (Mouse.current != null && Mouse.current.rightButton.isPressed);

                if (canLook)
                {
                    Vector2 look = Core.InputManager.Instance != null
                        ? Core.InputManager.Instance.LookInput
                        : (Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero);

                    _targetYaw += look.x * _sensitivity;
                    _targetPitch = Mathf.Clamp(_targetPitch - look.y * _sensitivity, _minPitch, _maxPitch);
                }
            }

            // Smooth rotational damping for silky mouse look
            _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVel, _rotationSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            _pitch = Mathf.SmoothDampAngle(_pitch, _targetPitch, ref _pitchVel, _rotationSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            Vector3 targetPivot = GetWorldPivot();
            float smoothFactor = 1f - Mathf.Exp(-_followSmoothing * Time.deltaTime);
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, targetPivot, smoothFactor);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredDir = -(rotation * Vector3.forward);

            // Collision check (ignoring player & triggers)
            float desiredDist = _distance;
            RaycastHit[] hits = Physics.SphereCastAll(_smoothedPivot, _collisionRadius, desiredDir, _distance, _collisionMask, QueryTriggerInteraction.Ignore);
            float closestHit = _distance;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform ht = hits[i].transform;
                if (ht == _target || ht.IsChildOf(_target)) continue;
                if (hits[i].distance < closestHit)
                    closestHit = hits[i].distance;
            }

            if (closestHit < _distance)
            {
                desiredDist = Mathf.Max(_minCollisionDistance, closestHit - 0.15f);
                // Snap closer quickly when obstructed, ease out smoothly when clear
                _currentDistance = Mathf.Lerp(_currentDistance, desiredDist, 1f - Mathf.Exp(-28f * Time.unscaledDeltaTime));
            }
            else
            {
                _currentDistance = Mathf.Lerp(_currentDistance, desiredDist, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            }

            transform.position = _smoothedPivot + desiredDir * _currentDistance;
            transform.rotation = rotation;

            if (_cam != null)
            {
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetFOV, 1f - Mathf.Exp(-_fovSmoothing * Time.unscaledDeltaTime));
            }
        }

        private void HandlePresetAndZoomInput()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame)
            {
                CycleCameraPreset();
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    // Normalize scroll value across platforms (Windows returns 120 per notch)
                    float notches = Mathf.Abs(scrollY) > 10f ? scrollY / 120f : Mathf.Sign(scrollY);
                    _distance = Mathf.Clamp(_distance - notches * _zoomSpeed, _minDistance, _maxDistance);
                }
            }
        }

        public void CycleCameraPreset()
        {
            int next = ((int)_currentPreset + 1) % 3;
            SetCameraPreset((CameraViewPreset)next, instant: false);

            if (UI.HUDController.Instance != null)
            {
                string label = _currentPreset switch
                {
                    CameraViewPreset.Exploration => "Exploration Vista (FOV 58° | 5.8m)",
                    CameraViewPreset.CloseAction => "Close Action Over-Shoulder (FOV 52° | 3.6m)",
                    CameraViewPreset.PanoramicTactical => "Panoramic Tactical View (FOV 64° | 9.5m)",
                    _ => _currentPreset.ToString()
                };
                UI.HUDController.Instance.ShowToast($"Camera View [V]: {label}", 2.2f);
            }
        }

        public void SetCameraPreset(CameraViewPreset preset, bool instant = false)
        {
            _currentPreset = preset;
            ApplyPresetValues(preset, instant);
        }

        private void ApplyPresetValues(CameraViewPreset preset, bool instant)
        {
            switch (preset)
            {
                case CameraViewPreset.Exploration:
                    _pivotOffset = new Vector3(0.35f, 1.75f, 0f);
                    _distance = 5.8f;
                    _targetFOV = 58f;
                    if (instant) _targetPitch = _pitch = 14f;
                    break;

                case CameraViewPreset.CloseAction:
                    _pivotOffset = new Vector3(0.50f, 1.62f, 0f);
                    _distance = 3.6f;
                    _targetFOV = 52f;
                    if (instant) _targetPitch = _pitch = 10f;
                    break;

                case CameraViewPreset.PanoramicTactical:
                    _pivotOffset = new Vector3(0.0f, 2.10f, 0f);
                    _distance = 9.5f;
                    _targetFOV = 64f;
                    if (instant) _targetPitch = _pitch = 22f;
                    break;
            }

            if (instant)
            {
                _currentDistance = _distance;
                if (_cam == null) _cam = GetComponent<Camera>();
                if (_cam != null) _cam.fieldOfView = _targetFOV;
            }
        }

        private Vector3 GetWorldPivot()
        {
            if (_target == null) return transform.position;
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            return _target.position + new Vector3(0f, _pivotOffset.y, 0f) + yawRot * new Vector3(_pivotOffset.x, 0f, _pivotOffset.z);
        }

        public void ApplyInitialFraming(Transform targetTransform = null, float yawDegrees = 14f, float pitchDegrees = 14f, CameraViewPreset preset = CameraViewPreset.Exploration)
        {
            if (targetTransform != null) _target = targetTransform;
            _targetYaw = _yaw = yawDegrees;
            _targetPitch = _pitch = pitchDegrees;
            SetCameraPreset(preset, instant: true);

            _smoothedPivot = GetWorldPivot();
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _smoothedPivot - rotation * Vector3.forward * _distance;
            transform.rotation = rotation;

            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _cam.fieldOfView = _targetFOV;
                _cam.nearClipPlane = 0.15f;
                _cam.farClipPlane = 450f;
            }
        }
    }
}
