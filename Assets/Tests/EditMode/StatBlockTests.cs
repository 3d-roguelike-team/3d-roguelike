using NUnit.Framework;
using Roguelike.Core;

namespace Roguelike.Tests.EditMode
{
    public class StatBlockTests
    {
        [Test]
        public void Get_WithNoModifiers_ReturnsBaseValue()
        {
            var stats = new StatBlock();
            stats.SetBase(StatType.MaxHealth, 100f);

            Assert.AreEqual(100f, stats.Get(StatType.MaxHealth), 0.001f);
            Assert.AreEqual(0f, stats.Get(StatType.Damage), 0.001f);
        }

        [Test]
        public void Get_StacksFlatPercentAddAndPercentMultInOrder()
        {
            var stats = new StatBlock();
            stats.SetBase(StatType.Damage, 10f);

            // Flat: 10 + 5 = 15
            stats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.Flat, 5f));

            // PercentAdd sums together: +20% + +30% = +50% -> 15 * 1.5 = 22.5
            stats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.PercentAdd, 0.20f));
            stats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.PercentAdd, 0.30f));

            // PercentMult multiplies independently: 22.5 * 1.1 * 1.2 = 29.7
            stats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.PercentMult, 0.10f));
            stats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.PercentMult, 0.20f));

            Assert.AreEqual(29.7f, stats.Get(StatType.Damage), 0.001f);
        }

        [Test]
        public void RemoveAllFrom_RemovesOnlyMatchingSourceModifiers()
        {
            var stats = new StatBlock();
            stats.SetBase(StatType.FireRate, 5f);

            var barrelAttachment = new object();
            var scopeAttachment = new object();

            stats.AddModifier(new StatModifier(StatType.FireRate, ModifierKind.Flat, 2f), barrelAttachment);
            stats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.Flat, 10f), barrelAttachment);
            stats.AddModifier(new StatModifier(StatType.FireRate, ModifierKind.Flat, 3f), scopeAttachment);

            stats.RemoveAllFrom(barrelAttachment);

            // Scope's +3 FireRate remains (5 + 3 = 8); barrel's modifiers are gone
            Assert.AreEqual(8f, stats.Get(StatType.FireRate), 0.001f);
            Assert.AreEqual(0f, stats.Get(StatType.Damage), 0.001f);
        }

        [Test]
        public void Changed_FiresOnBaseAndModifierUpdates()
        {
            var stats = new StatBlock();
            int fireCount = 0;
            StatType lastChanged = default;

            stats.Changed += stat =>
            {
                fireCount++;
                lastChanged = stat;
            };

            var source = new object();
            stats.SetBase(StatType.MoveSpeed, 6f);
            stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierKind.Flat, 1f), source);
            stats.RemoveAllFrom(source);

            Assert.AreEqual(3, fireCount);
            Assert.AreEqual(StatType.MoveSpeed, lastChanged);
        }
    }
}
