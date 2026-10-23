using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// ============================================================
//  EnemyBase — Base class for all enemies
//  Supports both baked NavMeshAgent navigation AND direct XZ
//  movement fallback when no NavMesh is baked yet.
// ============================================================
namespace EchoesOfTheLost.Characters.Enemies
{
    public enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Flee, Dead }

    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class EnemyBase : MonoBehaviour, EchoesOfTheLost.Systems.Combat.IDamageable
    {
        [Header("Enemy Info")]
        [SerializeField] protected string _enemyId = "enemy_guardian";
        [SerializeField] protected string _displayName = "Overgrown Guardian";

        [Header("Stats")]
        [SerializeField] protected float _maxHealth = 50f;
        [SerializeField] protected float _attackDamage = 8f;
        [SerializeField] protected float _defense = 2f;
        [SerializeField] protected float _moveSpeed = 3.2f;
        [SerializeField] protected float _runSpeed = 5.2f;

        [Header("Detection")]
        [SerializeField] protected float _viewDistance = 12f;
        [SerializeField] protected float _viewAngle = 120f;
        [SerializeField] protected float _hearingRadius = 7f;
        [SerializeField] protected LayerMask _playerLayer;
        [SerializeField] protected LayerMask _obstacleMask;

        [Header("Combat")]
        [SerializeField] protected float _attackRange = 2.0f;
        [SerializeField] protected float _attackCooldown = 1.6f;
        [SerializeField] protected float _fleeHealthThreshold = 0.15f;
        [SerializeField] protected bool _useTimedDamageFallback = true;

        [Header("Patrol")]
        [SerializeField] protected Transform[] _patrolPoints;
        [SerializeField] protected float _waypointTolerance = 0.6f;

        [Header("Loot & Rewards")]
        [SerializeField] protected float _xpReward = 35f;
        [SerializeField] protected float _goldReward = 15f;

        // ── State ─────────────────────────────────────────────
        protected EnemyState _currentState = EnemyState.Idle;
        protected float _currentHealth;
        protected bool _isDead;
        protected Transform _playerTransform;
        protected NavMeshAgent _agent;
        protected Animator _animator;
        protected float _attackTimer;
        protected int _currentPatrolIndex;
        protected Vector3 _spawnOrigin;
        protected Vector3 _lastFramePosition;
        protected float _currentMoveVelocity;

        // ── Public Properties ────────────────────────────────
        public string EnemyId => _enemyId;
        public string DisplayName => _displayName;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float HealthPercent => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;
        public EnemyState CurrentState => _currentState;

        public event System.Action<float, float> OnHealthChanged; // (current, max)

        // ── Animator Hashes ──────────────────────────────────
        protected static readonly int _animSpeed = Animator.StringToHash("Speed");
        protected static readonly int _animAttack = Animator.StringToHash("Attack");
        protected static readonly int _animDie = Animator.StringToHash("Die");
        protected static readonly int _animHit = Animator.StringToHash("Hit");

        protected virtual void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            _currentHealth = _maxHealth;
            _spawnOrigin = transform.position;
            _lastFramePosition = transform.position;
            if (_agent != null) _agent.speed = _moveSpeed;
        }

        protected virtual void Start()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;

            TransitionToState(EnemyState.Patrol);
        }

        protected virtual void Update()
        {
            if (_isDead) return;
            _attackTimer -= Time.deltaTime;

            UpdateState();
            UpdateAnimator();

            _currentMoveVelocity = (transform.position - _lastFramePosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastFramePosition = transform.position;
        }

        // ── State Machine ────────────────────────────────────

        protected virtual void UpdateState()
        {
            switch (_currentState)
            {
                case EnemyState.Patrol: UpdatePatrol(); break;
                case EnemyState.Alert: UpdateAlert(); break;
                case EnemyState.Chase: UpdateChase(); break;
                case EnemyState.Attack: UpdateAttack(); break;
                case EnemyState.Flee: UpdateFlee(); break;
            }
        }

        protected virtual void UpdatePatrol()
        {
            if (CanSeePlayer() || CanHearPlayer())
            {
                TransitionToState(EnemyState.Chase);
                return;
            }
            MoveToNextWaypoint();
        }

        protected virtual void UpdateAlert()
        {
            if (CanSeePlayer() || CanHearPlayer())
            {
                TransitionToState(EnemyState.Chase);
                return;
            }
            TransitionToState(EnemyState.Patrol);
        }

        protected virtual void UpdateChase()
        {
            if (_playerTransform == null) { TransitionToState(EnemyState.Patrol); return; }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);

            if (dist <= _attackRange)
            {
                TransitionToState(EnemyState.Attack);
                return;
            }

            if (!CanSeePlayer() && dist > _viewDistance * 1.5f)
            {
                TransitionToState(EnemyState.Patrol);
                return;
            }

            MoveTowardsTarget(_playerTransform.position, _runSpeed);
        }

        protected virtual void UpdateAttack()
        {
            if (_playerTransform == null) { TransitionToState(EnemyState.Patrol); return; }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);

            if (dist > _attackRange * 1.25f)
            {
                TransitionToState(EnemyState.Chase);
                return;
            }

            // Face player on XZ
            Vector3 lookPos = new Vector3(_playerTransform.position.x, transform.position.y, _playerTransform.position.z);
            if ((lookPos - transform.position).sqrMagnitude > 0.001f)
                transform.LookAt(lookPos);

            if (_attackTimer <= 0f)
            {
                PerformAttack();
                _attackTimer = _attackCooldown;
            }
        }

        protected virtual void UpdateFlee()
        {
            if (_playerTransform == null) return;
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > _viewDistance * 1.5f)
            {
                TransitionToState(EnemyState.Patrol);
                return;
            }

            Vector3 fleeDir = (transform.position - _playerTransform.position).normalized;
            MoveTowardsTarget(transform.position + fleeDir * 8f, _moveSpeed);
        }

        protected virtual void TransitionToState(EnemyState newState)
        {
            _currentState = newState;
            if (_agent == null || !_agent.isOnNavMesh) return;

            switch (newState)
            {
                case EnemyState.Chase:
                    _agent.speed = _runSpeed;
                    break;
                case EnemyState.Patrol:
                case EnemyState.Flee:
                    _agent.speed = _moveSpeed;
                    break;
                case EnemyState.Attack:
                    _agent.ResetPath();
                    break;
            }
        }

        // ── Navigation Helper (NavMesh + Direct Fallback) ────

        protected void MoveTowardsTarget(Vector3 targetPos, float speed)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.speed = speed;
                _agent.SetDestination(targetPos);
                return;
            }

            Vector3 flatTarget = new Vector3(targetPos.x, transform.position.y, targetPos.z);
            Vector3 dir = (flatTarget - transform.position);
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }

        // ── Perception ────────────────────────────────────────

        protected bool CanSeePlayer()
        {
            if (_playerTransform == null) return false;
            Vector3 toPlayer = _playerTransform.position - transform.position;
            float dist = toPlayer.magnitude;

            if (dist > _viewDistance) return false;
            if (Vector3.Angle(transform.forward, toPlayer.normalized) > _viewAngle * 0.5f) return false;
            if (_obstacleMask.value != 0 &&
                Physics.Raycast(transform.position + Vector3.up, toPlayer.normalized, dist, _obstacleMask))
                return false;

            return true;
        }

        protected bool CanHearPlayer()
        {
            if (_playerTransform == null) return false;
            return Vector3.Distance(transform.position, _playerTransform.position) <= _hearingRadius;
        }

        // ── Combat ───────────────────────────────────────────

        protected virtual void PerformAttack()
        {
            _animator.SetTrigger(_animAttack);
            if (_useTimedDamageFallback)
                StartCoroutine(DelayedDamageRoutine(0.25f));
        }

        private IEnumerator DelayedDamageRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_isDead) DealDamage();
        }

        public virtual void DealDamage()
        {
            if (_isDead || _playerTransform == null) return;
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > _attackRange * 1.35f) return;

            var stats = _playerTransform.GetComponent<Player.PlayerStats>();
            stats?.TakeDamage(_attackDamage);
        }

        // ── IDamageable ──────────────────────────────────────
        public bool IsDead => _isDead;

        public void TakeDamage(EchoesOfTheLost.Systems.Combat.DamageInfo info)
        {
            TakeDamage(info.amount, info.ignoreDefense);
        }

        public virtual void TakeDamage(float damage, bool ignoreDefense = false)
        {
            if (_isDead) return;
            float actual = ignoreDefense ? damage : Mathf.Max(1f, damage - _defense);
            _currentHealth = Mathf.Max(0f, _currentHealth - actual);
            _animator.SetTrigger(_animHit);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            StartCoroutine(DamageFlashRoutine());

            if (_currentHealth <= 0f)
            {
                Die();
                return;
            }

            if (_currentHealth / _maxHealth <= _fleeHealthThreshold)
                TransitionToState(EnemyState.Flee);
            else
                TransitionToState(EnemyState.Chase);
        }

        private IEnumerator DamageFlashRoutine()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var origColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material.HasProperty("_BaseColor"))
                {
                    origColors[i] = renderers[i].material.GetColor("_BaseColor");
                    renderers[i].material.SetColor("_BaseColor", Color.white);
                }
                else if (renderers[i].material.HasProperty("_Color"))
                {
                    origColors[i] = renderers[i].material.color;
                    renderers[i].material.color = Color.white;
                }
            }

            yield return new WaitForSeconds(0.1f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].material.HasProperty("_BaseColor"))
                    renderers[i].material.SetColor("_BaseColor", origColors[i]);
                else if (renderers[i].material.HasProperty("_Color"))
                    renderers[i].material.color = origColors[i];
            }
        }

        protected virtual void Die()
        {
            _isDead = true;
            if (_agent != null) _agent.enabled = false;
            _animator.SetTrigger(_animDie);

            Core.EventBus.Publish(new Core.EnemyDiedEvent
            {
                EnemyId = _enemyId,
                Position = transform.position
            });

            // Give XP & Gold to player
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            playerGO?.GetComponent<Player.PlayerStats>()?.AddXP(Mathf.RoundToInt(_xpReward));

            if (_goldReward > 0f && Core.ServiceLocator.TryGet<Systems.Economy.EconomySystem>(out var econ))
                econ.AddGold(_goldReward);

            // Drop loot table if LootSystem attached
            if (TryGetComponent<Systems.Combat.LootSystem>(out var loot))
                loot.DropLoot(transform.position);

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Visual collapse
            transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y * 0.35f, transform.localScale.z);
            Destroy(gameObject, 2.5f);
        }

        // ── Patrol ────────────────────────────────────────────
        private void MoveToNextWaypoint()
        {
            if (_patrolPoints == null || _patrolPoints.Length == 0) return;
            var target = _patrolPoints[_currentPatrolIndex];
            if (target == null) return;

            MoveTowardsTarget(target.position, _moveSpeed);

            Vector2 flatPos = new Vector2(transform.position.x, transform.position.z);
            Vector2 flatTarget = new Vector2(target.position.x, target.position.z);
            if (Vector2.Distance(flatPos, flatTarget) <= _waypointTolerance)
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
            }
        }

        // ── Animator ─────────────────────────────────────────
        private void UpdateAnimator()
        {
            float speed = (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                ? _agent.velocity.magnitude
                : _currentMoveVelocity;
            _animator.SetFloat(_animSpeed, speed);
        }

        // ── Debug Gizmos ─────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, _viewDistance);

            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, _attackRange);

            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawSphere(transform.position, _hearingRadius);
        }
    }
}
