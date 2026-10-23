using UnityEngine;

// ============================================================
//  ThirdPersonCamera — Simple orbit camera driven by InputManager look input.
//  Placeholder until Cinemachine is added to the project.
//  Attach to: Main Camera
// ============================================================
namespace EchoesOfTheLost.Characters.Player
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _pivotOffset = new Vector3(0f, 1.6f, 0f);

        [Header("Orbit")]
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _minPitch = -30f;
        [SerializeField] private float _maxPitch = 70f;
        [SerializeField] private float _sensitivity = 1f;
        [SerializeField] private float _followSmoothing = 12f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursor = true;

        private float _yaw;
        private float _pitch = 15f;
        private Vector3 _smoothedPivot;

        private void Start()
        {
            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _target = player.transform;
            }

            if (_target != null)
            {
                _yaw = _target.eulerAngles.y;
                _smoothedPivot = _target.position + _pivotOffset;
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

            var input = Core.InputManager.Instance;
            if (input != null)
            {
                Vector2 look = input.LookInput;
                _yaw += look.x * _sensitivity;
                _pitch = Mathf.Clamp(_pitch - look.y * _sensitivity, _minPitch, _maxPitch);
            }

            Vector3 pivot = _target.position + _pivotOffset;
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, pivot, 1f - Mathf.Exp(-_followSmoothing * Time.deltaTime));

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _smoothedPivot - rotation * Vector3.forward * _distance;
            transform.rotation = rotation;
        }
    }
}
