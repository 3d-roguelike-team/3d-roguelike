using System;

namespace Roguelike.Core
{
    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public ModifierKind Kind;
        public float Value;

        public StatModifier(StatType stat, ModifierKind kind, float value)
        {
            Stat = stat;
            Kind = kind;
            Value = value;
        }
    }
}
