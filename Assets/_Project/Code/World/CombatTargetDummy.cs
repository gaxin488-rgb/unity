using System.Collections;
using UnityEngine;
using EchoesOfTheLost.Systems.Combat;

// ============================================================================
//  PIPELINE C: ATTACKING, SOUND EFFECTS & ANIMATION TUTORIAL
//  Assets/_Project/Code/World/CombatTargetDummy.cs
//
//  Sparring target dummy placed in Verda Wilds to test:
//    - Combo attacks (Combo 1 -> Combo 2 -> Combo 3 -> Combo 4)
//    - Heavy attacks and critical hits
//    - Sound effects synchronization (whoosh, flesh/wood impact, hit-stop)
//    - Procedural squash-and-stretch wobble animation upon impact
// ============================================================================
namespace EchoesOfTheLost.World
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class CombatTargetDummy : MonoBehaviour, IDamageable
    {
        [Header("Dummy Stats")]
        [SerializeField] private float _maxHealth = 250f;
        [SerializeField] private float _currentHealth = 250f;
        [SerializeField] private bool _autoRegenerate = true;
        [SerializeField] private float _regenDelaySeconds = 2.5f;

        [Header("Visual Wobble & Hit Animation")]
        [SerializeField] private Transform _dummyBodyTransform;
        [SerializeField] private Renderer _dummyRenderer;
        [SerializeField] private Color _normalColor = new Color(0.72f, 0.52f, 0.32f);
        [SerializeField] private Color _flashColor = new Color(1.0f, 0.25f, 0.22f);
        [SerializeField] [Range(5f, 35f)] private float _wobbleMaxAngle = 18f;

        [Header("Hit Sound Effects")]
        [SerializeField] private AudioClip _impactSound;
        [SerializeField] private AudioSource _audioSource;

        private Vector3 _originalScale = Vector3.one;
        private Quaternion _originalRotation = Quaternion.identity;
        private Coroutine _hitReactionCoroutine;
        private Coroutine _regenCoroutine;
        private float _lastHitTime;

        public bool IsDead => false; // Sparring dummy never permanently dies
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;

        private void Awake()
        {
            if (_dummyBodyTransform == null) _dummyBodyTransform = transform;
            _originalScale = _dummyBodyTransform.localScale;
            _originalRotation = _dummyBodyTransform.localRotation;
            _currentHealth = _maxHealth;

            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0.85f;
                _audioSource.playOnAwake = false;
            }
        }

        public void ConfigureDummy(Transform body, Renderer rend, AudioClip impactClip, Color normalColor)
        {
            _dummyBodyTransform = body;
            _dummyRenderer = rend;
            _impactSound = impactClip;
            _normalColor = normalColor;
            if (_dummyBodyTransform != null)
            {
                _originalScale = _dummyBodyTransform.localScale;
                _originalRotation = _dummyBodyTransform.localRotation;
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            _currentHealth = Mathf.Max(0f, _currentHealth - info.amount);
            _lastHitTime = Time.time;

            // Play hit audio
            if (_impactSound != null && _audioSource != null)
            {
                _audioSource.pitch = Random.Range(0.92f, 1.08f);
                _audioSource.PlayOneShot(_impactSound, 0.9f);
            }

            // Trigger hit animation
            if (gameObject.activeInHierarchy)
            {
                if (_hitReactionCoroutine != null) StopCoroutine(_hitReactionCoroutine);
                _hitReactionCoroutine = StartCoroutine(HitSquashAndWobbleRoutine(info.hitNormal));

                if (_autoRegenerate)
                {
                    if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
                    _regenCoroutine = StartCoroutine(RegenHealthRoutine());
                }
            }

            Debug.Log($"[CombatTargetDummy] Struck! Damage: {info.amount:F1} (Crit: {info.isCritical}) | HP: {_currentHealth:F0}/{_maxHealth}");
        }

        private IEnumerator HitSquashAndWobbleRoutine(Vector3 hitDirection)
        {
            Vector3 pushAxis = Vector3.Cross(Vector3.up, hitDirection.sqrMagnitude > 0.01f ? hitDirection : transform.forward).normalized;
            if (pushAxis.sqrMagnitude < 0.01f) pushAxis = transform.right;

            // Flash red
            if (_dummyRenderer != null && _dummyRenderer.sharedMaterial != null)
            {
                _dummyRenderer.material.color = _flashColor;
            }

            float duration = 0.38f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Damped sine wave wobble
                float wobble = Mathf.Sin(progress * Mathf.PI * 4f) * Mathf.Exp(-progress * 5.5f);
                float angle = wobble * _wobbleMaxAngle;
                _dummyBodyTransform.localRotation = _originalRotation * Quaternion.AngleAxis(angle, pushAxis);

                // Squash & stretch: compress Y, bulge XZ
                float squash = 1f - (Mathf.Sin(progress * Mathf.PI) * 0.18f);
                float bulge = 1f + (Mathf.Sin(progress * Mathf.PI) * 0.12f);
                _dummyBodyTransform.localScale = new Vector3(_originalScale.x * bulge, _originalScale.y * squash, _originalScale.z * bulge);

                yield return null;
            }

            _dummyBodyTransform.localRotation = _originalRotation;
            _dummyBodyTransform.localScale = _originalScale;

            if (_dummyRenderer != null && _dummyRenderer.sharedMaterial != null)
            {
                _dummyRenderer.material.color = _normalColor;
            }
            _hitReactionCoroutine = null;
        }

        private IEnumerator RegenHealthRoutine()
        {
            yield return new WaitForSeconds(_regenDelaySeconds);

            while (_currentHealth < _maxHealth)
            {
                _currentHealth = Mathf.Min(_maxHealth, _currentHealth + 35f * Time.deltaTime);
                yield return null;
            }
            _regenCoroutine = null;
        }
    }
}
