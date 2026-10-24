using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoesOfTheLost.Core
{
    [DefaultExecutionOrder(-100)]
    public class InputManager : Utils.Singleton<InputManager>
    {
        [Header("Input Asset")]
        [SerializeField] private InputActionAsset _inputAsset;

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

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprintHeld { get; private set; }
        public bool IsBlockHeld { get; private set; }

        private bool _inputEnabled = true;
        private Vector2 _lastMoveInput;

        protected override void Awake()
        {
            base.Awake();
            // Tắt asset gốc nếu đang bật dở để tránh lỗi "Map index on InputActionMap is out of range" trên Unity 6
            if (_inputAsset != null)
            {
                try { _inputAsset.Disable(); } catch { }
            }
        }

        private void Update()
        {
            if (!_inputEnabled)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // 1. Đọc di chuyển WASD / Phím mũi tên trực tiếp
            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 1f) move.Normalize();
            }

            MoveInput = move;
            if (move != _lastMoveInput)
            {
                _lastMoveInput = move;
                OnMoveInput?.Invoke(move);
            }

            // 2. Đọc chuyển động chuột (Look) trực tiếp
            if (mouse != null)
            {
                LookInput = mouse.delta.ReadValue();
                if (LookInput.sqrMagnitude > 0.0001f)
                    OnLookInput?.Invoke(LookInput);
            }
            else
            {
                LookInput = Vector2.zero;
            }

            // 3. Đọc các phím chức năng
            if (kb != null)
            {
                bool sprintNow = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (sprintNow != IsSprintHeld)
                {
                    IsSprintHeld = sprintNow;
                    OnSprintHeld?.Invoke(sprintNow);
                }

                if (kb.spaceKey.wasPressedThisFrame) OnJumpPressed?.Invoke();
                if (kb.leftCtrlKey.wasPressedThisFrame) OnRollPressed?.Invoke();
                if (kb.cKey.wasPressedThisFrame) OnCrouchPressed?.Invoke();
                if (kb.eKey.wasPressedThisFrame) OnInteractPressed?.Invoke();
                if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) OnInventoryPressed?.Invoke();
                if (kb.mKey.wasPressedThisFrame) OnMapPressed?.Invoke();
                if (kb.jKey.wasPressedThisFrame) OnJournalPressed?.Invoke();
                if (kb.escapeKey.wasPressedThisFrame) OnPausePressed?.Invoke();
            }

            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) OnAttackLightPressed?.Invoke();
                if (mouse.rightButton.wasPressedThisFrame) OnAttackHeavyPressed?.Invoke();

                bool blockNow = mouse.rightButton.isPressed;
                if (blockNow && !IsBlockHeld) OnBlockHeld?.Invoke();
                IsBlockHeld = blockNow;
            }
        }

        public void DisablePlayerInput()
        {
            _inputEnabled = false;
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
        }

        public void EnablePlayerInput()
        {
            _inputEnabled = true;
        }
    }
}
