using UnityEngine;

namespace Roguelike.Combat
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Roguelike/Combat/Weapon Definition")]
    /// <summary>
    /// Defines the configuration and base combat stats for a weapon.
    /// </summary>
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName = "Rifle";
        [SerializeField] private GameObject _prefab;

        [Header("Base Stats")]
        // Damage dealt by each successful shot before modifiers or critical hits.
        [SerializeField] private float _damage = 20f;

        // Number of shots the weapon can fire per second.
        [SerializeField] private float _fireRate = 8f;

        // Number of shots available before the weapon must reload.
        [SerializeField] private int _magazineSize = 30;

        // Time required to refill the magazine, in seconds.
        [SerializeField] private float _reloadTime = 1.5f;

        // Probability of a shot becoming a critical hit, represented as a value from 0 to 1.
        [Range(0f, 1f)]
        [SerializeField] private float _critChance = 0.05f;

        // Damage multiplier applied when a shot is critical.
        [SerializeField] private float _critMultiplier = 2f;

        // Damage multiplier applied when a shot hits a weak point.
        [SerializeField] private float _weakpointMultiplier = 2f;

        [Header("Ballistics")]
        // Maximum distance at which a hitscan shot can register a hit.
        [SerializeField] private float _maxRange = 100f;

        // Layers that can be detected by the weapon's raycast.
        [SerializeField] private LayerMask _hitMask;

        public string DisplayName => _displayName;
        public GameObject Prefab => _prefab;
        public float Damage => _damage;
        public float FireRate => _fireRate;
        public int MagazineSize => _magazineSize;
        public float ReloadTime => _reloadTime;
        public float CritChance => _critChance;
        public float CritMultiplier => _critMultiplier;
        public float WeakpointMultiplier => _weakpointMultiplier;
        public float MaxRange => _maxRange;
        public LayerMask HitMask => _hitMask;
    }
}
