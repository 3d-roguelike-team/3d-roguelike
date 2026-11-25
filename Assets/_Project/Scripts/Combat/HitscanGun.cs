using System;
using Roguelike.Core;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Fires weapons using an instant raycast from the aim origin to the target.
    /// </summary>
    public class HitscanGun : MonoBehaviour, IWeapon
    {
        [Header("Configuration")]
        // Supplies the weapon's base stats and ballistic settings.
        [SerializeField] private WeaponDefinition _definition;

        // Usually the camera or muzzle transform used to determine the shot direction.
        [SerializeField] private Transform _aimOrigin;

        // Layers whose colliders count as weakpoints when hit.
        [SerializeField] private LayerMask _weakpointMask;

        // Can be shared with another weapon or owner so modifiers affect this weapon consistently.
        private StatBlock _stats;
        private int _currentAmmo;
        private bool _isReloading;
        private float _nextFireTime;
        private float _reloadCompleteTime;
        private bool _isInitialized;

        public WeaponDefinition Definition => _definition;
        public StatBlock Stats => _stats;
        public int CurrentAmmo => _currentAmmo;
        public int MaxAmmo => Mathf.Max(1, Mathf.RoundToInt(_stats.Get(StatType.MagazineSize)));
        public bool IsReloading => _isReloading;

        // Raised after a shot is fired, including shots that do not hit a target.
        public event Action Fired;

        // Raised when a reload begins and completes when the magazine is refilled.
        public event Action ReloadStarted;
        public event Action ReloadCompleted;

        // Provides hit information for effects, UI, and other weapon listeners.
        public event Action<RaycastHit, float, bool, bool> TargetHit;

        private void Awake()
        {
            // Initialize weapons configured directly in the Inspector.
            if (!_isInitialized && _definition != null)
            {
                Initialize(_definition);
            }
        }

        private void Update()
        {
            // Reloads use a completion timestamp so they can finish without a coroutine.
            if (_isReloading && Time.time >= _reloadCompleteTime)
            {
                CompleteReload();
            }
        }

        public void Initialize(WeaponDefinition definition, StatBlock sharedStatBlock = null)
        {
            _definition = definition;
            _stats = sharedStatBlock ?? CreateDefaultStatBlock(definition);
            _currentAmmo = MaxAmmo;
            _isReloading = false;
            _nextFireTime = 0f;
            _isInitialized = true;
        }

        /// <summary>
        /// Sets the transform used as the origin and direction for future shots.
        /// </summary>
        public void SetAimOrigin(Transform aimOrigin)
        {
            _aimOrigin = aimOrigin;
        }

        /// <summary>
        /// Attempts to fire one shot if the weapon is ready and has ammunition.
        /// </summary>
        public void TryFire()
        {
            // Ignore fire requests until the weapon has been initialized.
            if (!_isInitialized || _definition == null)
            {
                return;
            }

            if (_isReloading || Time.time < _nextFireTime)
            {
                return;
            }

            if (_currentAmmo <= 0)
            {
                Reload();
                return;
            }

            float fireRate = Mathf.Max(0.01f, _stats.Get(StatType.FireRate));
            _nextFireTime = Time.time + (1f / fireRate);
            _currentAmmo--;

            // Consume ammo before performing the shot so every successful fire request costs one round.
            PerformHitscan();
            Fired?.Invoke();
        }

        /// <summary>
        /// Starts reloading when the weapon is not already full or reloading.
        /// </summary>
        public void Reload()
        {
            if (!_isInitialized || _definition == null)
            {
                return;
            }

            if (_isReloading || _currentAmmo >= MaxAmmo)
            {
                return;
            }

            float reloadDuration = Mathf.Max(0.05f, _stats.Get(StatType.ReloadTime));
            _isReloading = true;
            _reloadCompleteTime = Time.time + reloadDuration;
            ReloadStarted?.Invoke();
        }

        private void CompleteReload()
        {
            // Complete the reload and notify listeners such as the weapon UI.
            _isReloading = false;
            _currentAmmo = MaxAmmo;
            ReloadCompleted?.Invoke();
        }

        private void PerformHitscan()
        {
            // Use the configured aim origin, falling back to this object's transform if needed.
            Transform origin = _aimOrigin != null ? _aimOrigin : transform;
            Ray ray = new Ray(origin.position, origin.forward);

            // The raycast uses the weapon definition's range and hit layers.
            if (!Physics.Raycast(ray, out RaycastHit hit, _definition.MaxRange, _definition.HitMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            // Weakpoints and critical hits are separate multipliers and can both apply.
            bool isWeakpoint = (_weakpointMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            float critChance = Mathf.Clamp01(_stats.Get(StatType.CritChance));
            bool isCrit = critChance > 0f && UnityEngine.Random.value < critChance;

            float damage = _stats.Get(StatType.Damage);
            if (isWeakpoint)
            {
                damage *= _definition.WeakpointMultiplier;
            }

            if (isCrit)
            {
                damage *= _definition.CritMultiplier;
            }

            // Search the parent hierarchy so child colliders resolve the root IDamageable.
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage, hit.point, hit.normal, this);
            }

            TargetHit?.Invoke(hit, damage, isWeakpoint, isCrit);
        }

        private static StatBlock CreateDefaultStatBlock(WeaponDefinition definition)
        {
            // Copy definition values into the runtime stat block so modifiers can be applied safely.
            var stats = new StatBlock();
            stats.SetBase(StatType.Damage, definition.Damage);
            stats.SetBase(StatType.FireRate, definition.FireRate);
            stats.SetBase(StatType.MagazineSize, definition.MagazineSize);
            stats.SetBase(StatType.ReloadTime, definition.ReloadTime);
            stats.SetBase(StatType.CritChance, definition.CritChance);
            return stats;
        }
    }
}
