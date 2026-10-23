using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoesOfTheLost.Characters.Player
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("First-Person Camera & Viewmodel References")]
        [SerializeField] private Transform _eyeMountTransform;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private ThirdPersonCamera _thirdPersonCamera;
        [SerializeField] private GameObject _thirdPersonBodyModel;
        [SerializeField] private GameObject _firstPersonViewModel;

        [Header("First-Person Look & Movement Settings")]
        [SerializeField] private bool _isFirstPersonActive;
        [SerializeField] [Range(0.05f, 1.0f)] private float _lookSensitivity = 0.16f;
        [SerializeField] [Range(-89f, -30f)] private float _minPitchDegrees = -80f;
        [SerializeField] [Range(30f, 89f)] private float _maxPitchDegrees = 80f;
        [SerializeField] [Range(55f, 85f)] private float _baseFirstPersonFov = 64f;
        [SerializeField] [Range(60f, 95f)] private float _sprintFirstPersonFov = 71f;

        [Header("Procedural Head-Bob & Weapon Sway")]
        [SerializeField] [Range(4f, 18f)] private float _headBobFrequency = 10.5f;
        [SerializeField] [Range(0.01f, 0.12f)] private float _headBobAmplitude = 0.040f;
        [SerializeField] private Vector3 _eyeBaseLocalPosition = new Vector3(0f, 1.68f, 0.12f);

        private float _yawDegrees;
        private float _pitchDegrees = 4f;
        private float _headBobTimer;
        private PlayerController _playerController;

        public bool IsFirstPersonActive => _isFirstPersonActive;
        public Transform EyeMountTransform => _eyeMountTransform;
        public GameObject FirstPersonViewModel => _firstPersonViewModel;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
        }

        public void ConfigureReferences(
            Transform eyeMount,
            Camera mainCam,
            ThirdPersonCamera thirdPersonCam,
            GameObject thirdPersonBody,
            GameObject fpViewModel)
        {
            _eyeMountTransform = eyeMount;
            _mainCamera = mainCam;
            _thirdPersonCamera = thirdPersonCam;
            _thirdPersonBodyModel = thirdPersonBody;
            _firstPersonViewModel = fpViewModel;

            if (_eyeMountTransform != null)
                _eyeMountTransform.localPosition = _eyeBaseLocalPosition;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (Core.ServiceLocator.TryGet<UI.UIManager>(out var ui) && ui != null && ui.IsWindowOpen)
                return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.cKey.wasPressedThisFrame)
                {
                    float startYaw = _thirdPersonCamera != null ? _thirdPersonCamera.CurrentYaw : transform.eulerAngles.y;
                    float startPitch = _thirdPersonCamera != null ? _thirdPersonCamera.CurrentPitch : 0f;
                    SetFirstPersonMode(!_isFirstPersonActive, startYaw, startPitch);

                    if (UI.HUDController.Instance != null)
                    {
                        string modeName = _isFirstPersonActive ? "First-Person View [C]" : "Third-Person Orbit View [V]";
                        UI.HUDController.Instance.ShowToast($"Camera Mode: {modeName}", 2.0f);
                    }
                }
                else if (kb.vKey.wasPressedThisFrame && _isFirstPersonActive)
                {
                    SetFirstPersonMode(false, _yawDegrees, _pitchDegrees);
                }
            }

            if (!_isFirstPersonActive) return;

            UpdateFirstPersonLook();
            UpdateHeadBobAndWeaponSway(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!_isFirstPersonActive) return;
            SyncCameraToEyeMount();
        }

        public void SetFirstPersonMode(bool enableFirstPerson, float customYaw = 12f, float customPitch = 3f)
        {
            _isFirstPersonActive = enableFirstPerson;
            _yawDegrees = customYaw;
            _pitchDegrees = Mathf.Clamp(customPitch, _minPitchDegrees, _maxPitchDegrees);

            if (_thirdPersonCamera != null)
            {
                _thirdPersonCamera.enabled = !enableFirstPerson;
                if (!enableFirstPerson)
                {
                    _thirdPersonCamera.SetAngles(_yawDegrees, Mathf.Clamp(_pitchDegrees, -20f, 65f));
                }
            }

            if (_thirdPersonBodyModel != null)
                _thirdPersonBodyModel.SetActive(!enableFirstPerson);

            if (_firstPersonViewModel != null)
                _firstPersonViewModel.SetActive(enableFirstPerson);

            if (enableFirstPerson)
            {
                transform.rotation = Quaternion.Euler(0f, _yawDegrees, 0f);
                if (_eyeMountTransform != null)
                {
                    _eyeMountTransform.localPosition = _eyeBaseLocalPosition;
                    _eyeMountTransform.localRotation = Quaternion.Euler(_pitchDegrees, 0f, 0f);
                }
                SyncCameraToEyeMount();
                if (_mainCamera != null)
                    _mainCamera.fieldOfView = _baseFirstPersonFov;
            }
        }

        private void UpdateFirstPersonLook()
        {
            bool canLook = Cursor.lockState == CursorLockMode.Locked ||
                           (Mouse.current != null && Mouse.current.rightButton.isPressed);
            if (!canLook) return;

            Vector2 lookDelta = Core.InputManager.Instance != null
                ? Core.InputManager.Instance.LookInput
                : (Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero);

            _yawDegrees += lookDelta.x * _lookSensitivity;
            _pitchDegrees = Mathf.Clamp(_pitchDegrees - lookDelta.y * _lookSensitivity, _minPitchDegrees, _maxPitchDegrees);

            transform.rotation = Quaternion.Euler(0f, _yawDegrees, 0f);
            if (_eyeMountTransform != null)
            {
                _eyeMountTransform.localRotation = Quaternion.Euler(_pitchDegrees, 0f, 0f);
            }
        }

        private void UpdateHeadBobAndWeaponSway(float deltaTime)
        {
            if (_eyeMountTransform == null) return;

            Vector2 moveInput = Core.InputManager.Instance != null
                ? Core.InputManager.Instance.MoveInput
                : Vector2.zero;
            bool isMoving = moveInput.sqrMagnitude > 0.02f;
            bool isSprinting = _playerController != null && _playerController.IsSprinting;

            if (isMoving)
            {
                float freq = isSprinting ? _headBobFrequency * 1.35f : _headBobFrequency;
                _headBobTimer += deltaTime * freq;
                float bobY = Mathf.Sin(_headBobTimer) * _headBobAmplitude;
                float bobX = Mathf.Cos(_headBobTimer * 0.5f) * (_headBobAmplitude * 0.5f);
                _eyeMountTransform.localPosition = _eyeBaseLocalPosition + new Vector3(bobX, bobY, 0f);
            }
            else
            {
                _headBobTimer = 0f;
                _eyeMountTransform.localPosition = Vector3.Lerp(
                    _eyeMountTransform.localPosition,
                    _eyeBaseLocalPosition,
                    10f * deltaTime);
            }

            if (_mainCamera != null)
            {
                float targetFov = isSprinting && isMoving ? _sprintFirstPersonFov : _baseFirstPersonFov;
                _mainCamera.fieldOfView = Mathf.Lerp(_mainCamera.fieldOfView, targetFov, 8f * deltaTime);
            }
        }

        private void SyncCameraToEyeMount()
        {
            if (_mainCamera == null || _eyeMountTransform == null) return;
            _mainCamera.transform.position = _eyeMountTransform.position;
            _mainCamera.transform.rotation = _eyeMountTransform.rotation;
        }
    }
}
