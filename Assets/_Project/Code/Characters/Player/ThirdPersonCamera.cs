using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
//  ThirdPersonCamera — Action-RPG Orbit Camera for Echoes of the Lost
//  Features:
//    - 3 Camera View Presets ([V] to cycle: Exploration / Close Action / Panoramic Tactical)
//    - Mouse Scroll-Wheel Zoom (2.5m .. 12m)
//    - Over-the-Shoulder Pivot Offset & Smooth Damping
//    - SphereCast Obstacle & Terrain Collision Avoidance
// ============================================================
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
        [SerializeField] private float _minDistance = 2.4f;
        [SerializeField] private float _maxDistance = 12.0f;
        [SerializeField] private float _zoomSpeed = 0.8f;
        [SerializeField] private float _minPitch = -22f;
        [SerializeField] private float _maxPitch = 68f;
        [SerializeField] private float _sensitivity = 0.18f;
        [SerializeField] private float _followSmoothing = 14f;

        [Header("Field of View")]
        [SerializeField] private float _targetFOV = 58f;
        [SerializeField] private float _fovSmoothing = 8f;

        [Header("Collision Avoidance")]
        [SerializeField] private float _collisionRadius = 0.28f;
        [SerializeField] private float _minCollisionDistance = 0.9f;
        [SerializeField] private LayerMask _collisionMask = ~0;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursor = true;

        private float _yaw = 14f;
        private float _pitch = 14f;
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
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            HandlePresetAndZoomInput();

            var input = Core.InputManager.Instance;
            if (input != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = input.LookInput;
                _yaw += look.x * _sensitivity;
                _pitch = Mathf.Clamp(_pitch - look.y * _sensitivity, _minPitch, _maxPitch);
            }

            Vector3 targetPivot = GetWorldPivot();
            float smoothFactor = 1f - Mathf.Exp(-_followSmoothing * Time.unscaledDeltaTime);
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, targetPivot, smoothFactor);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredDir = -(rotation * Vector3.forward);

            // Collision check so camera never clips inside terrain or ruin walls
            float desiredDist = _distance;
            if (Physics.SphereCast(_smoothedPivot, _collisionRadius, desiredDir, out RaycastHit hit, _distance, _collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(_target))
                {
                    desiredDist = Mathf.Max(_minCollisionDistance, hit.distance - 0.15f);
                }
            }

            _currentDistance = Mathf.Lerp(_currentDistance, desiredDist, smoothFactor);
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
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    _distance = Mathf.Clamp(_distance - Mathf.Sign(scrollY) * _zoomSpeed, _minDistance, _maxDistance);
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
                UI.HUDController.Instance.ShowToast($"Camera View [V]: {label}", 2.6f);
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
                    if (instant) _pitch = 14f;
                    break;

                case CameraViewPreset.CloseAction:
                    _pivotOffset = new Vector3(0.55f, 1.60f, 0f);
                    _distance = 3.6f;
                    _targetFOV = 52f;
                    if (instant) _pitch = 10f;
                    break;

                case CameraViewPreset.PanoramicTactical:
                    _pivotOffset = new Vector3(0.0f, 2.10f, 0f);
                    _distance = 9.5f;
                    _targetFOV = 64f;
                    if (instant) _pitch = 23f;
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

        /// <summary>
        /// Immediately positions and aligns the camera in Edit Mode or Play Mode for optimal framing.
        /// </summary>
        public void ApplyInitialFraming(Transform targetTransform, float yawDegrees = 14f, float pitchDegrees = 14f, CameraViewPreset preset = CameraViewPreset.Exploration)
        {
            _target = targetTransform;
            _yaw = yawDegrees;
            _pitch = pitchDegrees;
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
