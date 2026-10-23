using UnityEngine;
using System.Collections;

// ============================================================
//  PlayerController — Rigidbody-based third-person movement
//  Attach to: Player root GameObject
//  Requires: Rigidbody, CapsuleCollider, Animator, PlayerStats
// ============================================================
namespace EchoesOfTheLost.Characters.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _runSpeed = 7f;
        [SerializeField] private float _crouchSpeed = 2.5f;
        [SerializeField] private float _swimSpeed = 3f;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private float _acceleration = 10f;

        [Header("Jump & Gravity")]
        [SerializeField] private float _jumpForce = 7f;
        [SerializeField] private float _gravityMultiplier = 2.5f;
        [SerializeField] private float _groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Roll / Dodge")]
        [SerializeField] private float _rollForce = 8f;
        [SerializeField] private float _rollDuration = 0.4f;
        [SerializeField] private float _rollStaminaCost = 20f;
        [SerializeField] private float _rollCooldown = 0.8f;

        [Header("Stamina Costs")]
        [SerializeField] private float _sprintStaminaCost = 15f; // per second
        [SerializeField] private float _jumpStaminaCost = 10f;

        [Header("Interaction")]
        [SerializeField] private float _interactRange = 2.5f;
        [SerializeField] private LayerMask _interactLayer;

        [Header("Camera Reference")]
        [SerializeField] private Transform _cameraTransform;

        // ── Components ────────────────────────────────────────
        private Rigidbody _rb;
        private Animator _animator;
        private PlayerStats _stats;

        // ── State ─────────────────────────────────────────────
        private Vector2 _moveInput;
        private bool _isSprinting;
        private bool _isGrounded;
        private bool _isCrouching;
        private bool _isRolling;
        private bool _isSwimming;
        private float _rollTimer;
        private float _rollCooldownTimer;
        private Vector3 _moveDirection;
        private float _currentSpeed;

        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _isSprinting;
        public bool IsCrouching => _isCrouching;
        public bool IsRolling => _isRolling;
        public bool IsSwimming => _isSwimming;

        // ── Animator Hashes ──────────────────────────────────
        private static readonly int _animSpeed = Animator.StringToHash("Speed");
        private static readonly int _animGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int _animCrouch = Animator.StringToHash("IsCrouching");
        private static readonly int _animJump = Animator.StringToHash("Jump");
        private static readonly int _animRoll = Animator.StringToHash("Roll");
        private static readonly int _animSwim = Animator.StringToHash("IsSwimming");

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _stats = GetComponent<PlayerStats>();

            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            var input = Core.InputManager.Instance;
            if (input == null) return;

            input.OnMoveInput += OnMove;
            input.OnJumpPressed += OnJump;
            input.OnSprintHeld += OnSprint;
            input.OnRollPressed += OnRoll;
            input.OnCrouchPressed += OnCrouch;
            // InteractionSystem handles OnInteractPressed when present on Player
            if (!TryGetComponent<InteractionSystem>(out _))
                input.OnInteractPressed += OnInteract;
        }

        private void OnDisable()
        {
            var input = Core.InputManager.Instance;
            if (input == null) return;

            input.OnMoveInput -= OnMove;
            input.OnJumpPressed -= OnJump;
            input.OnSprintHeld -= OnSprint;
            input.OnRollPressed -= OnRoll;
            input.OnCrouchPressed -= OnCrouch;
            input.OnInteractPressed -= OnInteract;
        }

        private void Update()
        {
            CheckGrounded();
            HandleSprintStamina();
            UpdateRollCooldown();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (!_isRolling)
                MoveCharacter();
            ApplyGravity();
        }

        // ── Movement ─────────────────────────────────────────

        private void MoveCharacter()
        {
            if (_moveInput.magnitude < 0.1f)
            {
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                return;
            }

            // Camera-relative movement direction
            Vector3 camForward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            _moveDirection = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;

            // Determine target speed
            float targetSpeed = _isCrouching ? _crouchSpeed
                              : _isSwimming ? _swimSpeed
                              : _isSprinting && _stats.CurrentStamina > 0f ? _runSpeed
                              : _walkSpeed;

            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, _acceleration * Time.fixedDeltaTime);

            // Apply movement
            Vector3 velocity = _moveDirection * _currentSpeed;
            velocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocity;

            // Rotate to face movement direction
            if (_moveDirection != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(_moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSpeed * Time.fixedDeltaTime);
            }
        }

        private void ApplyGravity()
        {
            if (!_isGrounded)
                _rb.AddForce(Physics.gravity * (_gravityMultiplier - 1f), ForceMode.Acceleration);
        }

        // ── Input Handlers ────────────────────────────────────

        private void OnMove(Vector2 input) => _moveInput = input;
        private void OnSprint(bool held) => _isSprinting = held;

        private void OnJump()
        {
            if (!_isGrounded || _isCrouching) return;
            if (!_stats.UseStamina(_jumpStaminaCost)) return;

            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.VelocityChange);
            _animator.SetTrigger(_animJump);
        }

        private void OnRoll()
        {
            if (_isRolling || _rollCooldownTimer > 0f) return;
            if (!_stats.UseStamina(_rollStaminaCost)) return;

            StartCoroutine(PerformRoll());
        }

        private IEnumerator PerformRoll()
        {
            _isRolling = true;
            _animator.SetTrigger(_animRoll);

            Vector3 rollDir = _moveDirection != Vector3.zero
                ? _moveDirection : transform.forward;

            float elapsed = 0f;
            while (elapsed < _rollDuration)
            {
                _rb.linearVelocity = new Vector3(rollDir.x * _rollForce, _rb.linearVelocity.y, rollDir.z * _rollForce);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _isRolling = false;
            _rollCooldownTimer = _rollCooldown;
        }

        private void OnCrouch()
        {
            if (!_isGrounded) return;
            _isCrouching = !_isCrouching;
            _animator.SetBool(_animCrouch, _isCrouching);
        }

        private void OnInteract()
        {
            // Sphere overlap to find nearest interactable
            var hits = Physics.OverlapSphere(transform.position, _interactRange, _interactLayer);
            float nearest = float.MaxValue;
            IInteractable bestTarget = null;

            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < nearest && hit.TryGetComponent<IInteractable>(out var interactable))
                {
                    nearest = dist;
                    bestTarget = interactable;
                }
            }

            bestTarget?.Interact(this);
        }

        // ── Helpers ───────────────────────────────────────────

        private void CheckGrounded()
        {
            _isGrounded = Physics.SphereCast(
                transform.position + Vector3.up * 0.5f,
                0.4f,
                Vector3.down,
                out _,
                _groundCheckDistance + 0.5f,
                _groundLayer);
        }

        private void HandleSprintStamina()
        {
            if (_isSprinting && _moveInput.magnitude > 0.1f)
                _stats.UseStamina(_sprintStaminaCost * Time.deltaTime);
        }

        private void UpdateRollCooldown()
        {
            if (_rollCooldownTimer > 0f) _rollCooldownTimer -= Time.deltaTime;
        }

        private void UpdateAnimator()
        {
            float speed = _rb.linearVelocity.magnitude;
            _animator.SetFloat(_animSpeed, speed, 0.1f, Time.deltaTime);
            _animator.SetBool(_animGrounded, _isGrounded);
            _animator.SetBool(_animSwim, _isSwimming);
        }
    }

    // ── Interactable Interface ────────────────────────────────
    public interface IInteractable
    {
        void Interact(PlayerController player);
        string GetInteractionPrompt();
    }
}
