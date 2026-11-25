using System.Reflection;
using NUnit.Framework;
using Roguelike.Core;
using Roguelike.Core.Events;
using Roguelike.Enemies;
using UnityEngine;

namespace Roguelike.Tests.EditMode
{
    public class EnemyHealthTests
    {
        private GameObject _enemyObject;
        private EnemyDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            SetPrivateField(_definition, "_displayName", "Test Dummy");
            SetPrivateField(_definition, "_tier", EnemyTier.Elite);
            SetPrivateField(_definition, "_maxHealth", 100f);
            SetPrivateField(_definition, "_damage", 15f);
            SetPrivateField(_definition, "_moveSpeed", 4f);

            _enemyObject = new GameObject("TestEnemy");
            _enemyObject.transform.position = new Vector3(3f, 0f, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null)
            {
                Object.DestroyImmediate(_enemyObject);
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        [Test]
        public void Initialize_PopulatesStatsAndCurrentHealthFromDefinition()
        {
            var health = CreateInitializedHealth(destroyOnDeath: false);

            Assert.AreEqual(100f, health.MaxHealth, 0.001f);
            Assert.AreEqual(100f, health.CurrentHealth, 0.001f);
            Assert.AreEqual(15f, health.Stats.Get(StatType.Damage), 0.001f);
            Assert.AreEqual(4f, health.Stats.Get(StatType.MoveSpeed), 0.001f);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void TakeDamage_ReducesHealthAndRaisesEnemyKilledOnceOnDeath()
        {
            var health = CreateInitializedHealth(destroyOnDeath: false);
            int killedCount = 0;
            EnemyKilled lastKilled = default;

            void OnEnemyKilled(EnemyKilled evt)
            {
                killedCount++;
                lastKilled = evt;
            }

            EventBus<EnemyKilled>.Subscribe(OnEnemyKilled);

            try
            {
                health.TakeDamage(40f, Vector3.zero, Vector3.up);
                Assert.AreEqual(60f, health.CurrentHealth, 0.001f);
                Assert.IsFalse(health.IsDead);
                Assert.AreEqual(0, killedCount);

                // Lethal hit
                health.TakeDamage(80f, Vector3.zero, Vector3.up);
                Assert.AreEqual(0f, health.CurrentHealth, 0.001f);
                Assert.IsTrue(health.IsDead);
                Assert.AreEqual(1, killedCount);
                Assert.AreEqual(new Vector3(3f, 0f, 5f), lastKilled.Position);
                Assert.AreEqual(EnemyTier.Elite, lastKilled.Tier);

                // Extra damage after death must not re-trigger EnemyKilled
                health.TakeDamage(50f, Vector3.zero, Vector3.up);
                Assert.AreEqual(1, killedCount);
            }
            finally
            {
                EventBus<EnemyKilled>.Unsubscribe(OnEnemyKilled);
            }
        }

        [Test]
        public void StatBlockModifiers_ScaleEnemyMaxHealth()
        {
            var health = CreateInitializedHealth(destroyOnDeath: false);
            health.Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentMult, 0.50f));

            Assert.AreEqual(150f, health.MaxHealth, 0.001f);
        }

        private EnemyHealth CreateInitializedHealth(bool destroyOnDeath)
        {
            var health = _enemyObject.AddComponent<EnemyHealth>();
            SetPrivateField(health, "_destroyOnDeath", destroyOnDeath);
            health.Initialize(_definition);
            return health;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
