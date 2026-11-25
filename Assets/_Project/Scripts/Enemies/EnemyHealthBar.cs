using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Enemies
{
    /// <summary>
    /// Displays an enemy's current health and optionally rotates the bar toward the camera.
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("References")]
        // Health component that supplies the current and maximum health values.
        [SerializeField] private EnemyHealth _enemyHealth;

        // UI image whose fill amount represents the health percentage.
        [SerializeField] private Image _fillImage;

        // Root object containing the health bar visuals, used for visibility changes.
        [SerializeField] private GameObject _visualRoot;

        [Header("Behavior")]
        // Hides the bar while the enemy is at full health.
        [SerializeField] private bool _hideWhenFull = true;

        // Keeps the bar facing the camera so it remains readable in world space.
        [SerializeField] private bool _billboardToCamera = true;

        // Cached to avoid repeatedly looking up the main camera during LateUpdate.
        private Camera _cachedCamera;

        private void Awake()
        {
            // Allow the health component to be assigned explicitly or found on a parent object.
            if (_enemyHealth == null)
            {
                _enemyHealth = GetComponentInParent<EnemyHealth>();
            }
        }

        private void OnEnable()
        {
            // Subscribe to health events and immediately synchronize the initial display state.
            if (_enemyHealth != null)
            {
                _enemyHealth.HealthChanged += OnHealthChanged;
                _enemyHealth.Died += OnDied;
                OnHealthChanged(_enemyHealth.CurrentHealth, _enemyHealth.MaxHealth);
            }
        }

        private void OnDisable()
        {
            // Unsubscribe so this UI element does not receive events while disabled or destroyed.
            if (_enemyHealth != null)
            {
                _enemyHealth.HealthChanged -= OnHealthChanged;
                _enemyHealth.Died -= OnDied;
            }
        }

        private void LateUpdate()
        {
            // Skip the billboard calculation when the feature is disabled.
            if (!_billboardToCamera)
            {
                return;
            }

            if (_cachedCamera == null)
            {
                // Cache the camera the first time it is needed instead of searching every frame.
                _cachedCamera = Camera.main;
                if (_cachedCamera == null)
                {
                    return;
                }
            }

            // Rotate around the vertical axis so the health bar faces the camera without tilting.
            Vector3 dir = _cachedCamera.transform.position - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            // Convert the raw health values into the 0-to-1 range expected by Image.fillAmount.
            float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (_fillImage != null)
            {
                _fillImage.fillAmount = normalized;
            }

            if (_visualRoot != null)
            {
                // Hide the bar at full health when configured, and always hide it after death.
                bool shouldShow = current > 0f && (!_hideWhenFull || normalized < 0.999f);
                _visualRoot.SetActive(shouldShow);
            }
        }

        private void OnDied()
        {
            // Ensure the visuals disappear immediately when the enemy dies.
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(false);
            }
        }
    }
}
