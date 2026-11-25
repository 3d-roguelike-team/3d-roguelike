using Roguelike.Core;
using UnityEngine;

namespace Roguelike.Enemies
{
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Roguelike/Enemies/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName = "Enemy";
        [SerializeField] private EnemyTier _tier = EnemyTier.Normal;
        [SerializeField] private GameObject _prefab;

        [Header("Base Stats")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _moveSpeed = 3.5f;

        public string DisplayName => _displayName;
        public EnemyTier Tier => _tier;
        public GameObject Prefab => _prefab;
        public float MaxHealth => _maxHealth;
        public float Damage => _damage;
        public float MoveSpeed => _moveSpeed;
    }
}
