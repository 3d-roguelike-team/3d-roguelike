using System;
using Roguelike.Core;
using Roguelike.Core.Events;
using UnityEngine;

namespace Roguelike.Enemies
{
    /// <summary>
    /// Tracks an enemy's health, applies incoming damage, and broadcasts death notifications.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        // The definition supplies the enemy's base stats and tier.
        [SerializeField] private EnemyDefinition _definition;

        // Allows enemies to remain in the scene after death for animation or pooling.
        [SerializeField] private bool _destroyOnDeath = true;

        // Stats are stored in StatBlock so modifiers can affect enemy values.
        private readonly StatBlock _stats = new StatBlock();
        private float _currentHealth;
        private bool _isInitialized;

        public StatBlock Stats => _stats;
        public EnemyDefinition Definition => _definition;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _stats.Get(StatType.MaxHealth);
        public bool IsDead => _currentHealth <= 0f;

        // Used by health bars, hit effects, and other local enemy components.
        public event Action<float, float> HealthChanged;

        // Raised when this enemy finishes dying.
        public event Action Died;

        private void Awake()
        {
            // Initialize enemies configured directly in the Inspector.
            if (!_isInitialized && _definition != null)
            {
                Initialize(_definition);
            }
        }

        private void OnEnable()
        {
            // React when a modifier changes one of the enemy's stats.
            _stats.Changed += OnStatChanged;
        }

        private void OnDisable()
        {
            _stats.Changed -= OnStatChanged;
        }

        /// <summary>
        /// Loads the definition's base stats and starts the enemy at full health.
        /// </summary>
        public void Initialize(EnemyDefinition definition)
        {
            _definition = definition;
            _stats.SetBase(StatType.MaxHealth, definition.MaxHealth);
            _stats.SetBase(StatType.Damage, definition.Damage);
            _stats.SetBase(StatType.MoveSpeed, definition.MoveSpeed);

            _currentHealth = MaxHealth;
            _isInitialized = true;
            HealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        public void HealToFull()
        {
            if (IsDead)
            {
                return;
            }

            _currentHealth = MaxHealth;
            HealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        /// <summary>
        /// Reduces health and starts the death sequence when health reaches zero.
        /// </summary>
        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, object source = null)
        {
            // Ignore invalid damage and prevent damage after the enemy has died.
            if (IsDead || amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            HealthChanged?.Invoke(_currentHealth, MaxHealth);

            if (IsDead)
            {
                Die();
            }
        }

        private void OnStatChanged(StatType stat)
        {
            // Only MaxHealth changes can affect the current health value.
            if (stat != StatType.MaxHealth || IsDead)
            {
                return;
            }

            float max = MaxHealth;

            // Prevent current health from exceeding a newly reduced maximum.
            if (_currentHealth > max)
            {
                _currentHealth = max;
            }

            HealthChanged?.Invoke(_currentHealth, max);
        }

        private void Die()
        {
            // Use the default tier if the definition is unavailable.
            EnemyTier tier = _definition != null ? _definition.Tier : EnemyTier.Normal;

            // Notify other modules through Core's event system.
            EventBus<EnemyKilled>.Raise(new EnemyKilled(transform.position, tier));
            Died?.Invoke();

            if (_destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }
}
