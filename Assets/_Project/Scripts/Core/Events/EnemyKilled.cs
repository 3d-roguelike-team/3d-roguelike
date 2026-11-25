using UnityEngine;

namespace Roguelike.Core.Events
{
    public readonly struct EnemyKilled
    {
        public readonly Vector3 Position;
        public readonly EnemyTier Tier;

        public EnemyKilled(Vector3 position, EnemyTier tier)
        {
            Position = position;
            Tier = tier;
        }
    }
}
