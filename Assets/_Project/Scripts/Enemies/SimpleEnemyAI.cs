using Roguelike.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike.Enemies
{
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(NavMeshAgent))]
    /// <summary>
    /// Finds the nearest valid target, moves toward it, and performs simple melee attacks.
    /// </summary>
    public class SimpleEnemyAI : MonoBehaviour
    {
        [Header("Detection & Melee Rush")]
        // Maximum distance at which this enemy can acquire or retain a target.
        [SerializeField] private float _aggroRange = 25f;

        // Distance at which movement stops and melee attacks can begin.
        [SerializeField] private float _attackRange = 1.8f;

        // Minimum time between attacks, in seconds.
        [SerializeField] private float _attackCooldown = 1.2f;

        // Layers searched when looking for possible targets.
        [SerializeField] private LayerMask _targetMask;

        // Required components used for health state and navigation.
        private EnemyHealth _health;
        private NavMeshAgent _agent;

        // The current target and its damage interface, if one was found.
        private Transform _target;
        private IDamageable _targetDamageable;
        private float _nextAttackTime;

        // Reused by OverlapSphereNonAlloc to avoid allocating an array every search.
        private readonly Collider[] _overlapBuffer = new Collider[8];

        public Transform CurrentTarget => _target;

        private void Awake()
        {
            // RequiredComponent attributes guarantee these components are available.
            _health = GetComponent<EnemyHealth>();
            _agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            // Stop navigation as soon as the health component reports death.
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
        }

        private void Start()
        {
            // Apply the definition's current, possibly modified, movement speed.
            SyncMoveSpeed();
        }

        private void Update()
        {
            // Dead enemies and disabled agents should not perform AI work.
            if (_health.IsDead || !_agent.enabled)
            {
                return;
            }

            SyncMoveSpeed();

            // Acquire a target only when the current target is missing or invalid.
            if (!HasValidTarget())
            {
                AcquireTarget();
                if (!HasValidTarget())
                {
                    if (_agent.isOnNavMesh && _agent.hasPath)
                    {
                        _agent.ResetPath();
                    }
                    return;
                }
            }

            float distance = Vector3.Distance(transform.position, _target.position);

            // Drop targets that have moved outside the enemy's detection range.
            if (distance > _aggroRange)
            {
                ClearTarget();
                return;
            }

            if (distance <= _attackRange)
            {
                // Stop before the target, face it, and attempt an attack on cooldown.
                if (_agent.isOnNavMesh)
                {
                    _agent.ResetPath();
                }

                FaceTarget();
                TryAttack();
            }
            else if (_agent.isOnNavMesh)
            {
                // Stop slightly short of the target so the agent can attack from range.
                _agent.stoppingDistance = Mathf.Max(0.1f, _attackRange * 0.85f);
                _agent.SetDestination(_target.position);
            }
        }

        /// <summary>
        /// Assigns a target and resolves the damage interface from its hierarchy.
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            _targetDamageable = target != null ? target.GetComponentInParent<IDamageable>() : null;
        }

        /// <summary>
        /// Applies difficulty multipliers to the enemy's runtime stats.
        /// </summary>
        public void ApplyDifficulty(IDifficultyProvider difficulty)
        {
            if (difficulty == null)
            {
                return;
            }

            float bonus = difficulty.Current - 1f;
            if (!Mathf.Approximately(bonus, 0f))
            {
                _health.Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentMult, bonus));
                _health.Stats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.PercentMult, bonus));
                _health.Stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierKind.PercentMult, bonus * 0.3f));
            }

            if (_health.Definition != null)
            {
                _health.HealToFull();
            }

            SyncMoveSpeed();
        }

        private void SyncMoveSpeed()
        {
            // Read through StatBlock so speed modifiers are reflected by the NavMeshAgent.
            float speed = _health.Stats.Get(StatType.MoveSpeed);
            if (speed > 0f)
            {
                _agent.speed = speed;
            }
        }

        private bool HasValidTarget()
        {
            // A target can become invalid when it is destroyed, disabled, or killed.
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (_targetDamageable != null && _targetDamageable.IsDead)
            {
                return false;
            }

            return true;
        }

        private void AcquireTarget()
        {
            // A zero mask means there are no layers eligible for target detection.
            if (_targetMask.value == 0)
            {
                return;
            }

            // Find nearby colliders without allocating a new collection.
            int hits = Physics.OverlapSphereNonAlloc(transform.position, _aggroRange, _overlapBuffer, _targetMask);
            float closestSqrDist = float.MaxValue;
            Transform bestTarget = null;

            for (int i = 0; i < hits; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                // Resolve damageable targets from parent objects so child colliders are supported.
                IDamageable candidate = col.GetComponentInParent<IDamageable>();
                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                float sqrDist = (col.transform.position - transform.position).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    bestTarget = col.transform;
                }
            }

            if (bestTarget != null)
            {
                // The closest valid target becomes the enemy's current target.
                SetTarget(bestTarget);
            }
        }

        private void TryAttack()
        {
            // The cooldown prevents an attack from occurring every frame.
            if (Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime = Time.time + _attackCooldown;

            // Recheck the target because it may have died between Update calls.
            if (_targetDamageable != null && !_targetDamageable.IsDead)
            {
                float damage = _health.Stats.Get(StatType.Damage);
                Vector3 hitPoint = _target.position;
                Vector3 hitNormal = (transform.position - _target.position).normalized;
                _targetDamageable.TakeDamage(damage, hitPoint, hitNormal, this);
            }
        }

        private void FaceTarget()
        {
            // Rotate only around the vertical axis so the enemy does not tilt toward the target.
            Vector3 direction = _target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void ClearTarget()
        {
            // Clear both references and stop any navigation toward the old target.
            _target = null;
            _targetDamageable = null;
            if (_agent.isOnNavMesh && _agent.hasPath)
            {
                _agent.ResetPath();
            }
        }

        private void OnDied()
        {
            // Disable navigation after death so the enemy cannot continue moving or attacking.
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
            _agent.enabled = false;
        }
    }
}
