using System;
using System.Collections.Generic;

namespace Roguelike.Core
{
    /// <summary>
    /// Pure C# container for base stats and sourced modifiers (attachments, buffs, upgrades).
    /// Owned by players, weapons, and enemies without MonoBehaviour dependencies.
    /// </summary>
    public class StatBlock
    {
        private readonly struct SourcedModifier
        {
            public readonly StatModifier Modifier;
            public readonly object Source;

            public SourcedModifier(StatModifier modifier, object source)
            {
                Modifier = modifier;
                Source = source;
            }
        }

        private readonly Dictionary<StatType, float> _baseValues = new();
        private readonly List<SourcedModifier> _modifiers = new();

        /// <summary>
        /// Fired whenever a stat's base value changes or a modifier affecting that stat is added or removed.
        /// </summary>
        public event Action<StatType> Changed;

        public void SetBase(StatType stat, float value)
        {
            _baseValues[stat] = value;
            Changed?.Invoke(stat);
        }

        public float GetBase(StatType stat)
        {
            return _baseValues.TryGetValue(stat, out float value) ? value : 0f;
        }

        /// <summary>
        /// Applies a modifier tagged with an optional <paramref name="source"/> object
        /// (such as an attachment or buff instance) so all modifiers from that source can be removed together later.
        /// </summary>
        public void AddModifier(StatModifier modifier, object source = null)
        {
            _modifiers.Add(new SourcedModifier(modifier, source));
            Changed?.Invoke(modifier.Stat);
        }

        /// <summary>
        /// Removes every modifier matching <paramref name="source"/> by reference and notifies listeners once per affected stat.
        /// </summary>
        public void RemoveAllFrom(object source)
        {
            if (source == null)
            {
                return;
            }

            HashSet<StatType> affectedStats = null;

            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_modifiers[i].Source, source))
                {
                    affectedStats ??= new HashSet<StatType>();
                    affectedStats.Add(_modifiers[i].Modifier.Stat);
                    _modifiers.RemoveAt(i);
                }
            }

            if (affectedStats != null)
            {
                foreach (StatType stat in affectedStats)
                {
                    Changed?.Invoke(stat);
                }
            }
        }

        /// <summary>
        /// Computes the final stat value using: (Base + sum(Flat)) * (1 + sum(PercentAdd)) * product(1 + PercentMult).
        /// </summary>
        public float Get(StatType stat)
        {
            float baseValue = GetBase(stat);
            float flatSum = 0f;
            float percentAddSum = 0f;
            float percentMultProduct = 1f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier mod = _modifiers[i].Modifier;
                if (mod.Stat != stat)
                {
                    continue;
                }

                switch (mod.Kind)
                {
                    case ModifierKind.Flat:
                        flatSum += mod.Value;
                        break;
                    case ModifierKind.PercentAdd:
                        percentAddSum += mod.Value;
                        break;
                    case ModifierKind.PercentMult:
                        percentMultProduct *= 1f + mod.Value;
                        break;
                }
            }

            return (baseValue + flatSum) * (1f + percentAddSum) * percentMultProduct;
        }
    }
}
