using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
//  InputManager — Wrapper for Unity New Input System
//  Attach to: _Boot scene persistent GameObject
//  Assign:    PlayerInputActions.inputactions to _inputAsset
//  Note: actions are looked up by name from the asset, so no
//        generated wrapper class is required.
// ============================================================
namespace EchoesOfTheLost.Core
{
    [DefaultExecutionOrder(-100)]
    public class InputManager : Utils.Singleton<InputManager>
    {
        [Header("Input Asset")]
        [SerializeField] private InputActionAsset _inputAsset;

        // ─── Events ──────────────────────────────────────────
        public event System.Action<Vector2> OnMoveInput;
        public event System.Action<Vector2> OnLookInput;
        public event System.Action OnJumpPressed;
        public event System.Action<bool> OnSprintHeld;
        public event System.Action OnRollPressed;
        public event System.Action OnInteractPressed;
        public event System.Action OnAttackLightPressed;
        public event System.Action OnAttackHeavyPressed;
        public event System.Action OnBlockHeld;
        public event System.Action OnInventoryPressed;
        public event System.Action OnMapPressed;
        public event System.Action OnJournalPressed;
        public event System.Action OnPausePressed;
        public event System.Action OnCrouchPressed;

        // ─── Public Accessors (current frame state) ─────────
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprintHeld { get; private set; }
        public bool IsBlockHeld { get; private set; }

        // ─── Private ─────────────────────────────────────────
        private InputActionAsset _runtimeAsset;
        private InputActionMap _player;
        private InputAction _move, _look, _jump, _sprint, _roll, _crouch, _interact;
        private InputAction _attackLight, _attackHeavy, _block, _inventory, _map, _journal, _pause;

        protected override void Awake()
        {
            base.Awake();

            if (_inputAsset == null)
            {
                Debug.LogError("[InputManager] Input asset is not assigned. Assign PlayerInputActions.inputactions in the inspector.", this);
                enabled = false;
                return;
            }

            // Per-instance copy so edits/rebinds never touch the project asset.
            _runtimeAsset = Instantiate(_inputAsset);
            _player = _runtimeAsset.FindActionMap("Player", throwIfNotFound: true);

            _move = _player.FindAction("Move", true);
            _look = _player.FindAction("Look", true);
            _jump = _player.FindAction("Jump", true);
            _sprint = _player.FindAction("Sprint", true);
            _roll = _player.FindAction("Roll", true);
            _crouch = _player.FindAction("Crouch", true);
            _interact = _player.FindAction("Interact", true);
            _attackLight = _player.FindAction("AttackLight", true);
            _attackHeavy = _player.FindAction("AttackHeavy", true);
            _block = _player.FindAction("Block", true);
            _inventory = _player.FindAction("Inventory", true);
            _map = _player.FindAction("Map", true);
            _journal = _player.FindAction("Journal", true);
            _pause = _player.FindAction("Pause", true);
        }

        private void OnEnable()
        {
            if (_player == null) return;
            BindActions();
            _player.Enable();
        }

        private void OnDisable()
        {
            if (_player == null) return;
            _player.Disable();
            UnbindActions();
        }

        protected override void OnDestroy()
        {
            if (_runtimeAsset != null) Destroy(_runtimeAsset);
            base.OnDestroy();
        }

        // ─── Binding ─────────────────────────────────────────

        private void BindActions()
        {
            _move.performed += OnMovePerformed;
            _move.canceled += OnMoveCanceled;
            _look.performed += OnLookPerformed;
            _look.canceled += OnLookCanceled;

            _jump.performed += OnJumpPerformed;
            _roll.performed += OnRollPerformed;
            _interact.performed += OnInteractPerformed;
            _crouch.performed += OnCrouchPerformed;
            _attackLight.performed += OnAttackLightPerformed;
            _attackHeavy.performed += OnAttackHeavyPerformed;

            _sprint.performed += OnSprintPerformed;
            _sprint.canceled += OnSprintCanceled;
            _block.performed += OnBlockPerformed;
            _block.canceled += OnBlockCanceled;

            _inventory.performed += OnInventoryPerformed;
            _map.performed += OnMapPerformed;
            _journal.performed += OnJournalPerformed;
            _pause.performed += OnPausePerformed;
        }

        private void UnbindActions()
        {
            _move.performed -= OnMovePerformed;
            _move.canceled -= OnMoveCanceled;
            _look.performed -= OnLookPerformed;
            _look.canceled -= OnLookCanceled;

            _jump.performed -= OnJumpPerformed;
            _roll.performed -= OnRollPerformed;
            _interact.performed -= OnInteractPerformed;
            _crouch.performed -= OnCrouchPerformed;
            _attackLight.performed -= OnAttackLightPerformed;
            _attackHeavy.performed -= OnAttackHeavyPerformed;

            _sprint.performed -= OnSprintPerformed;
            _sprint.canceled -= OnSprintCanceled;
            _block.performed -= OnBlockPerformed;
            _block.canceled -= OnBlockCanceled;

            _inventory.performed -= OnInventoryPerformed;
            _map.performed -= OnMapPerformed;
            _journal.performed -= OnJournalPerformed;
            _pause.performed -= OnPausePerformed;
        }

        // ─── Handlers ────────────────────────────────────────

        private void OnMovePerformed(InputAction.CallbackContext ctx) { MoveInput = ctx.ReadValue<Vector2>(); OnMoveInput?.Invoke(MoveInput); }
        private void OnMoveCanceled(InputAction.CallbackContext ctx) { MoveInput = Vector2.zero; OnMoveInput?.Invoke(Vector2.zero); }
        private void OnLookPerformed(InputAction.CallbackContext ctx) { LookInput = ctx.ReadValue<Vector2>(); OnLookInput?.Invoke(LookInput); }
        private void OnLookCanceled(InputAction.CallbackContext ctx) { LookInput = Vector2.zero; }

        private void OnJumpPerformed(InputAction.CallbackContext ctx) => OnJumpPressed?.Invoke();
        private void OnRollPerformed(InputAction.CallbackContext ctx) => OnRollPressed?.Invoke();
        private void OnInteractPerformed(InputAction.CallbackContext ctx) => OnInteractPressed?.Invoke();
        private void OnCrouchPerformed(InputAction.CallbackContext ctx) => OnCrouchPressed?.Invoke();
        private void OnAttackLightPerformed(InputAction.CallbackContext ctx) => OnAttackLightPressed?.Invoke();
        private void OnAttackHeavyPerformed(InputAction.CallbackContext ctx) => OnAttackHeavyPressed?.Invoke();

        private void OnSprintPerformed(InputAction.CallbackContext ctx) { IsSprintHeld = true; OnSprintHeld?.Invoke(true); }
        private void OnSprintCanceled(InputAction.CallbackContext ctx) { IsSprintHeld = false; OnSprintHeld?.Invoke(false); }
        private void OnBlockPerformed(InputAction.CallbackContext ctx) { IsBlockHeld = true; OnBlockHeld?.Invoke(); }
        private void OnBlockCanceled(InputAction.CallbackContext ctx) { IsBlockHeld = false; }

        private void OnInventoryPerformed(InputAction.CallbackContext ctx) => OnInventoryPressed?.Invoke();
        private void OnMapPerformed(InputAction.CallbackContext ctx) => OnMapPressed?.Invoke();
        private void OnJournalPerformed(InputAction.CallbackContext ctx) => OnJournalPressed?.Invoke();
        private void OnPausePerformed(InputAction.CallbackContext ctx) => OnPausePressed?.Invoke();

        // ─── Public Control ──────────────────────────────────

        /// <summary>Disable player input (e.g., during cutscenes).</summary>
        public void DisablePlayerInput() => _player?.Disable();

        /// <summary>Re-enable player input.</summary>
        public void EnablePlayerInput() => _player?.Enable();
    }
}
