using UnityEngine;

namespace Roguelike.Core
{
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }

        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, object source = null);
    }
}
