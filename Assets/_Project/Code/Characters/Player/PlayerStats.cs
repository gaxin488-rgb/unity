using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  PlayerStats — ScriptableObject-driven character stats
//  Attach to: Player GameObject
// ============================================================
namespace EchoesOfTheLost.Characters.Player
{
    [System.Serializable]
    public class StatValue
    {
        public float baseValue;
        private List<float> _modifiers = new List<float>();

        public float FinalValue
        {
            get
            {
                float total = baseValue;
                foreach (var m in _modifiers) total += m;
                return total;
            }
        }

        public void AddModifier(float modifier) => _modifiers.Add(modifier);
        public void RemoveModifier(float modifier) => _modifiers.Remove(modifier);
        public void ClearModifiers() => _modifiers.Clear();
    }

    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private float _baseMaxHealth = 100f;
        [SerializeField] private float _baseMaxStamina = 100f;
        [SerializeField] private float _baseAttackDamage = 10f;
        [SerializeField] private float _baseDefense = 5f;
        [SerializeField] private float _baseMoveSpeed = 5f;

        [Header("Level & Progression")]
        [SerializeField] private int _level = 1;
        [SerializeField] private int _currentXP = 0;
        [SerializeField] private AnimationCurve _xpToLevelCurve; // XP needed per level

        // ── Stat Values ──────────────────────────────────────
        public StatValue MaxHealth = new StatValue();
        public StatValue MaxStamina = new StatValue();
        public StatValue AttackDamage = new StatValue();
        public StatValue Defense = new StatValue();
        public StatValue MoveSpeed = new StatValue();

        // ── Current Values ───────────────────────────────────
        private float _currentHealth;
        private float _currentStamina;
        private bool _isDead;

        public float CurrentHealth => _currentHealth;
        public float CurrentStamina => _currentStamina;
        public float HealthPercent => _currentHealth / MaxHealth.FinalValue;
        public float StaminaPercent => _currentStamina / MaxStamina.FinalValue;
        public bool IsDead => _isDead;
        public int Level => _level;
        public int CurrentXP => _currentXP;

        [Header("Stamina Regen")]
        [SerializeField] private float _staminaRegenRate = 15f;
        [SerializeField] private float _staminaRegenDelay = 1.5f;
        private float _staminaRegenTimer;

        private void Awake()
        {
            // Init base values
            MaxHealth.baseValue = _baseMaxHealth;
            MaxStamina.baseValue = _baseMaxStamina;
            AttackDamage.baseValue = _baseAttackDamage;
            Defense.baseValue = _baseDefense;
            MoveSpeed.baseValue = _baseMoveSpeed;

            _currentHealth = MaxHealth.FinalValue;
            _currentStamina = MaxStamina.FinalValue;
        }

        private void Update()
        {
            RegenerateStamina();
        }

        // ── Health ────────────────────────────────────────────

        /// <summary>Apply damage. Returns actual damage dealt.</summary>
        public float TakeDamage(float rawDamage, bool ignoreDefense = false)
        {
            if (_isDead) return 0f;

            float actualDamage = ignoreDefense
                ? rawDamage
                : Mathf.Max(1f, rawDamage - Defense.FinalValue);

            _currentHealth = Mathf.Max(0f, _currentHealth - actualDamage);

            Core.EventBus.Publish(new Core.PlayerHealthChangedEvent
            {
                CurrentHealth = (int)_currentHealth,
                MaxHealth = (int)MaxHealth.FinalValue,
                Delta = -(int)actualDamage
            });

            if (_currentHealth <= 0f) Die();
            return actualDamage;
        }

        /// <summary>Heal the player.</summary>
        public void Heal(float amount)
        {
            if (_isDead) return;
            _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth.FinalValue);

            Core.EventBus.Publish(new Core.PlayerHealthChangedEvent
            {
                CurrentHealth = (int)_currentHealth,
                MaxHealth = (int)MaxHealth.FinalValue,
                Delta = (int)amount
            });
        }

        // ── Stamina ───────────────────────────────────────────

        /// <summary>Restore stamina.</summary>
        public void RestoreStamina(float amount)
        {
            if (_isDead) return;
            _currentStamina = Mathf.Min(_currentStamina + amount, MaxStamina.FinalValue);
        }

        /// <summary>Use stamina. Returns false if insufficient.</summary>
        public bool UseStamina(float amount)
        {
            if (_currentStamina < amount) return false;
            _currentStamina = Mathf.Max(0f, _currentStamina - amount);
            _staminaRegenTimer = _staminaRegenDelay;
            return true;
        }

        private void RegenerateStamina()
        {
            if (_staminaRegenTimer > 0f)
            {
                _staminaRegenTimer -= Time.deltaTime;
                return;
            }
            _currentStamina = Mathf.Min(_currentStamina + _staminaRegenRate * Time.deltaTime, MaxStamina.FinalValue);
        }

        // ── XP & Leveling ─────────────────────────────────────

        public void AddXP(int amount)
        {
            _currentXP += amount;
            CheckLevelUp();
        }

        private void CheckLevelUp()
        {
            int xpNeeded = GetXPForNextLevel();
            while (_currentXP >= xpNeeded)
            {
                _currentXP -= xpNeeded;
                _level++;
                OnLevelUp();
                xpNeeded = GetXPForNextLevel();
            }
        }

        private void OnLevelUp()
        {
            // Increase base stats on level up
            MaxHealth.baseValue += 10f;
            MaxStamina.baseValue += 5f;
            AttackDamage.baseValue += 2f;
            Defense.baseValue += 1f;

            // Restore to full
            _currentHealth = MaxHealth.FinalValue;
            _currentStamina = MaxStamina.FinalValue;

            Core.EventBus.Publish(new Core.PlayerLevelUpEvent { NewLevel = _level });
            Debug.Log($"[PlayerStats] Level up! → Level {_level}");
        }

        private int GetXPForNextLevel()
        {
            // An unassigned/empty curve evaluates to 0, which would make CheckLevelUp loop forever.
            // Fall back to a simple formula and never return less than 1.
            if (_xpToLevelCurve == null || _xpToLevelCurve.length == 0)
                return 100 * _level;

            return Mathf.Max(1, Mathf.RoundToInt(_xpToLevelCurve.Evaluate(_level)));
        }

        // ── Death ─────────────────────────────────────────────
        private void Die()
        {
            _isDead = true;
            Core.EventBus.Publish(new Core.PlayerDiedEvent());
            Core.GameManager.Instance?.SetGameState(Core.GameState.GameOver);
        }

        public void Revive(float healthPercent = 0.5f)
        {
            _isDead = false;
            _currentHealth = MaxHealth.FinalValue * healthPercent;
            _currentStamina = MaxStamina.FinalValue;
        }
    }
}
