using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  CombatManager — Hit detection, damage pipeline, combo system
//  Attach to: Player GameObject
// ============================================================
namespace EchoesOfTheLost.Systems.Combat
{
    public enum DamageType { Physical, Fire, Ice, Lightning, Arcane, True }

    [System.Serializable]
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public bool isCritical;
        public bool ignoreDefense;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public GameObject source;
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
        bool IsDead { get; }
    }

    public class CombatManager : MonoBehaviour
    {
        [Header("Combo Settings")]
        [SerializeField] private float _comboWindowDuration = 0.8f;  // time to input next combo
        [SerializeField] private int _maxComboSteps = 4;
        [Tooltip("Automatically triggers hitbox & combo window via timer when no Animation Events exist on placeholder clips.")]
        [SerializeField] private bool _useTimedHitboxFallback = true;

        private Coroutine _attackFallbackCoroutine;

        [Header("Attack Hitbox")]
        [SerializeField] private Transform _attackOrigin;
        [SerializeField] private float _lightAttackRange = 1.8f;
        [SerializeField] private float _heavyAttackRange = 2.2f;
        [SerializeField] private float _attackAngle = 120f;
        [SerializeField] private LayerMask _hitLayerMask;

        [Header("Parry / Block")]
        [SerializeField] private float _parryWindowDuration = 0.25f;   // perfect parry window
        [SerializeField] private float _blockDamageReduction = 0.5f;

        [Header("HitStop")]
        [SerializeField] private float _lightHitStopDuration = 0.05f;
        [SerializeField] private float _heavyHitStopDuration = 0.12f;

        [Header("Camera Shake")]
        [SerializeField] private float _lightHitShakeIntensity = 0.15f;
        [SerializeField] private float _heavyHitShakeIntensity = 0.4f;

        // ── Components ────────────────────────────────────────
        private Characters.Player.PlayerStats _playerStats;
        private Animator _animator;
        private Systems.Inventory.InventoryManager _inventory;

        // ── Combo State ───────────────────────────────────────
        private int _currentComboStep = 0;
        private float _comboWindowTimer = 0f;
        private bool _canCombo = false;
        private bool _isAttacking = false;
        private bool _isParrying = false;
        private bool _isBlocking = false;
        private float _parryTimer = 0f;
        private bool _hitboxActive = false;
        private bool _attackIsHeavy = false;

        // ── Hit tracking (per swing, avoid multi-hit same enemy) ──
        private HashSet<Collider> _hitTargetsThisSwing = new HashSet<Collider>();

        // ── Animator Hashes ──────────────────────────────────
        private static readonly int _animLightAttack = Animator.StringToHash("LightAttack");
        private static readonly int _animHeavyAttack = Animator.StringToHash("HeavyAttack");
        private static readonly int _animComboStep = Animator.StringToHash("ComboStep");
        private static readonly int _animParry = Animator.StringToHash("Parry");
        private static readonly int _animBlock = Animator.StringToHash("IsBlocking");

        public event System.Action<DamageInfo> OnAttackLanded;
        public event System.Action OnParrySuccess;
        public event System.Action OnComboFinished;

        public bool IsAttacking => _isAttacking;
        public int ComboStep => _currentComboStep;

        private void Awake()
        {
            _playerStats = GetComponent<Characters.Player.PlayerStats>();
            _animator = GetComponent<Animator>();
            _inventory = GetComponent<Systems.Inventory.InventoryManager>();
        }

        private void OnEnable()
        {
            var input = Core.InputManager.Instance;
            if (input == null) return;
            input.OnAttackLightPressed += OnLightAttack;
            input.OnAttackHeavyPressed += OnHeavyAttack;
        }

        private void OnDisable()
        {
            var input = Core.InputManager.Instance;
            if (input == null) return;
            input.OnAttackLightPressed -= OnLightAttack;
            input.OnAttackHeavyPressed -= OnHeavyAttack;
        }

        private void Update()
        {
            UpdateComboWindow();
            UpdateParry();
            UpdateBlocking();

            // Active hitbox check (called every frame while active)
            if (_hitboxActive) CheckHitbox();
        }

        // ── Input Handlers ────────────────────────────────────

        private void OnLightAttack()
        {
            if (_playerStats.IsDead) return;
            if (_isAttacking && !_canCombo) return;

            _attackIsHeavy = false;
            PerformAttack();
        }

        private void OnHeavyAttack()
        {
            if (_playerStats.IsDead) return;
            if (_isAttacking) return; // heavy cannot combo mid-chain

            _attackIsHeavy = true;
            _currentComboStep = 0; // reset combo for heavy
            PerformAttack();
        }

        // ── Attack Logic ─────────────────────────────────────

        private void PerformAttack()
        {
            _isAttacking = true;
            _canCombo = false;
            _currentComboStep = Mathf.Clamp(_currentComboStep + 1, 1, _maxComboSteps);
            _comboWindowTimer = 0f;

            _animator.SetInteger(_animComboStep, _currentComboStep);

            if (_attackIsHeavy)
                _animator.SetTrigger(_animHeavyAttack);
            else
                _animator.SetTrigger(_animLightAttack);

            _hitTargetsThisSwing.Clear();

            if (_useTimedHitboxFallback)
            {
                if (_attackFallbackCoroutine != null) StopCoroutine(_attackFallbackCoroutine);
                _attackFallbackCoroutine = StartCoroutine(TimedAttackFallbackRoutine(_attackIsHeavy));
            }
        }

        private IEnumerator TimedAttackFallbackRoutine(bool isHeavy)
        {
            yield return new WaitForSeconds(isHeavy ? 0.1f : 0.04f);
            EnableHitbox();

            // Subtle forward lunge for tactile feedback
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.AddForce(transform.forward * (isHeavy ? 4.5f : 2.5f), ForceMode.VelocityChange);

            yield return new WaitForSeconds(isHeavy ? 0.18f : 0.12f);
            DisableHitbox();
            OpenComboWindow();

            yield return new WaitForSeconds(isHeavy ? 0.35f : 0.25f);
            OnAttackAnimationEnd();
        }

        // ── Called by Animation Events ────────────────────────

        /// <summary>Enable hitbox — called at attack frame start via Animation Event.</summary>
        public void EnableHitbox()
        {
            _hitboxActive = true;
            _hitTargetsThisSwing.Clear();
        }

        /// <summary>Disable hitbox — called at attack frame end via Animation Event.</summary>
        public void DisableHitbox()
        {
            _hitboxActive = false;
        }

        /// <summary>Allow combo input — called mid-animation via Animation Event.</summary>
        public void OpenComboWindow()
        {
            _canCombo = true;
            _comboWindowTimer = _comboWindowDuration;
        }

        /// <summary>Attack animation finished — called at end of animation via Animation Event.</summary>
        public void OnAttackAnimationEnd()
        {
            if (!_canCombo)
            {
                // No combo input received — reset
                _isAttacking = false;
                _currentComboStep = 0;
                OnComboFinished?.Invoke();
            }
            _canCombo = false;
        }

        // ── Hitbox Detection ─────────────────────────────────

        private void CheckHitbox()
        {
            float range = _attackIsHeavy ? _heavyAttackRange : _lightAttackRange;
            Vector3 origin = _attackOrigin != null ? _attackOrigin.position : transform.position + Vector3.up;

            var hits = Physics.OverlapSphere(origin, range, _hitLayerMask);

            foreach (var hit in hits)
            {
                if (_hitTargetsThisSwing.Contains(hit)) continue;
                if (hit.transform.IsChildOf(transform)) continue; // skip self

                // Angle check
                Vector3 dir = (hit.transform.position - transform.position).normalized;
                if (Vector3.Angle(transform.forward, dir) > _attackAngle * 0.5f) continue;

                _hitTargetsThisSwing.Add(hit);
                ApplyDamageToTarget(hit);
            }
        }

        private void ApplyDamageToTarget(Collider targetCollider)
        {
            if (!targetCollider.TryGetComponent<IDamageable>(out var damageable)) return;
            if (damageable.IsDead) return;

            float baseDamage = _playerStats.AttackDamage.FinalValue;

            // Heavy attack bonus
            if (_attackIsHeavy) baseDamage *= 1.75f;

            // Critical hit
            bool isCrit = Random.value < 0.05f;
            if (isCrit) baseDamage *= 1.5f;

            var info = new DamageInfo
            {
                amount = baseDamage,
                type = DamageType.Physical,
                isCritical = isCrit,
                hitPoint = targetCollider.ClosestPoint(transform.position),
                hitNormal = (targetCollider.transform.position - transform.position).normalized,
                source = gameObject
            };

            damageable.TakeDamage(info);
            OnAttackLanded?.Invoke(info);

            // Game feel
            float stopDur = _attackIsHeavy ? _heavyHitStopDuration : _lightHitStopDuration;
            StartCoroutine(HitStop(stopDur));

            // Camera shake via Cinemachine Impulse (hookup from CameraManager)
            float shakeIntensity = _attackIsHeavy ? _heavyHitShakeIntensity : _lightHitShakeIntensity;
            CameraShakeSignal(shakeIntensity);
        }

        // ── Parry / Block ─────────────────────────────────────

        private void UpdateParry()
        {
            if (_parryTimer > 0f)
            {
                _parryTimer -= Time.deltaTime;
                _isParrying = _parryTimer > 0f;
            }
        }

        private void UpdateBlocking()
        {
            _isBlocking = Core.InputManager.Instance?.IsBlockHeld ?? false;
            _animator.SetBool(_animBlock, _isBlocking);
        }

        public void TriggerParryWindow()
        {
            _isParrying = true;
            _parryTimer = _parryWindowDuration;
            _animator.SetTrigger(_animParry);
        }

        /// <summary>Returns true if incoming damage was parried/blocked. Modifies DamageInfo.</summary>
        public bool TryAbsorbHit(ref DamageInfo info)
        {
            if (_isParrying)
            {
                OnParrySuccess?.Invoke();
                info.amount = 0f;
                // Stun attacker — handled externally
                return true;
            }

            if (_isBlocking)
            {
                info.amount *= (1f - _blockDamageReduction);
                return true;
            }

            return false;
        }

        // ── Combo Window ──────────────────────────────────────

        private void UpdateComboWindow()
        {
            if (_comboWindowTimer > 0f)
            {
                _comboWindowTimer -= Time.deltaTime;
                if (_comboWindowTimer <= 0f) _canCombo = false;
            }
        }

        // ── Game Feel ─────────────────────────────────────────

        private IEnumerator HitStop(float duration)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
        }

        private void CameraShakeSignal(float intensity)
        {
            // TODO: Connect to Cinemachine CinemachineImpulseSource
            // _impulseSource.GenerateImpulse(intensity);
        }

        // ── Gizmos ────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (_attackOrigin == null) return;
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(_attackOrigin.position, _lightAttackRange);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(_attackOrigin.position, _heavyAttackRange);
        }
    }
}
