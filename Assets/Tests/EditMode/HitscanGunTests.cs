using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Combat;
using Roguelike.Core;
using UnityEngine;

namespace Roguelike.Tests.EditMode
{
    public class HitscanGunTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private WeaponDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _createdObjects.Add(_definition);

            SetPrivateField(_definition, "_displayName", "Test Rifle");
            SetPrivateField(_definition, "_damage", 25f);
            SetPrivateField(_definition, "_fireRate", 10f);
            SetPrivateField(_definition, "_magazineSize", 3);
            SetPrivateField(_definition, "_reloadTime", 1.0f);
            SetPrivateField(_definition, "_critChance", 0f);
            SetPrivateField(_definition, "_critMultiplier", 2f);
            SetPrivateField(_definition, "_weakpointMultiplier", 2.5f);
            SetPrivateField(_definition, "_maxRange", 50f);
            SetPrivateField(_definition, "_hitMask", (LayerMask)(1 << 0)); // Default layer
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TryFire_ConsumesAmmoAndRespectsFireRateCooldown()
        {
            HitscanGun gun = CreateGun();

            Assert.AreEqual(3, gun.MaxAmmo);
            Assert.AreEqual(3, gun.CurrentAmmo);

            gun.TryFire();
            Assert.AreEqual(2, gun.CurrentAmmo);

            // Immediate second shot within cooldown window (1 / 10s = 0.1s) should not fire
            gun.TryFire();
            Assert.AreEqual(2, gun.CurrentAmmo);

            // Simulate cooldown elapsed
            SetPrivateField(gun, "_nextFireTime", Time.time - 0.01f);
            gun.TryFire();
            Assert.AreEqual(1, gun.CurrentAmmo);
        }

        [Test]
        public void Reload_RestoresMagazineAndBlocksFiringWhileReloading()
        {
            HitscanGun gun = CreateGun();

            gun.TryFire();
            Assert.AreEqual(2, gun.CurrentAmmo);

            gun.Reload();
            Assert.IsTrue(gun.IsReloading);

            // Firing while reloading must not consume ammo
            SetPrivateField(gun, "_nextFireTime", Time.time - 0.01f);
            gun.TryFire();
            Assert.AreEqual(2, gun.CurrentAmmo);

            // Simulate reload timer elapsed and invoke Update
            SetPrivateField(gun, "_reloadCompleteTime", Time.time - 0.01f);
            InvokePrivateMethod(gun, "Update");

            Assert.IsFalse(gun.IsReloading);
            Assert.AreEqual(3, gun.CurrentAmmo);
        }

        [Test]
        public void TryFire_RayHitsTargetWithWeakpointMask_AppliesMultiplierToIDamageable()
        {
            const int weakpointLayer = 2; // Built-in "Ignore Raycast" layer used safely as test mask
            SetPrivateField(_definition, "_hitMask", (LayerMask)(1 << weakpointLayer));

            HitscanGun gun = CreateGun();
            SetPrivateField(gun, "_weakpointMask", (LayerMask)(1 << weakpointLayer));

            // Create parent IDamageable target with child weakpoint collider
            var targetRoot = new GameObject("DummyTarget");
            _createdObjects.Add(targetRoot);
            targetRoot.transform.position = new Vector3(0f, 0f, 10f);
            var damageable = targetRoot.AddComponent<FakeDamageable>();

            var weakpointChild = new GameObject("WeakpointCollider");
            _createdObjects.Add(weakpointChild);
            weakpointChild.transform.SetParent(targetRoot.transform, false);
            weakpointChild.layer = weakpointLayer;
            weakpointChild.AddComponent<BoxCollider>();

            Physics.SyncTransforms();

            gun.TryFire();

            // 25 base damage * 2.5 weakpoint multiplier = 62.5
            Assert.AreEqual(1, damageable.HitCount);
            Assert.AreEqual(62.5f, damageable.LastDamage, 0.001f);
            Assert.AreSame(gun, damageable.LastSource);
        }

        [Test]
        public void SharedStatBlock_ScalesWeaponDamageAndMagazineSize()
        {
            var sharedStats = new StatBlock();
            sharedStats.SetBase(StatType.Damage, 20f);
            sharedStats.SetBase(StatType.FireRate, 5f);
            sharedStats.SetBase(StatType.MagazineSize, 10f);
            sharedStats.SetBase(StatType.ReloadTime, 2f);
            sharedStats.SetBase(StatType.CritChance, 0f);

            sharedStats.AddModifier(new StatModifier(StatType.MagazineSize, ModifierKind.Flat, 5f));
            sharedStats.AddModifier(new StatModifier(StatType.Damage, ModifierKind.PercentMult, 0.50f));

            HitscanGun gun = CreateGun(sharedStats);

            Assert.AreEqual(15, gun.MaxAmmo);
            Assert.AreEqual(15, gun.CurrentAmmo);
            Assert.AreEqual(30f, gun.Stats.Get(StatType.Damage), 0.001f);
        }

        private HitscanGun CreateGun(StatBlock sharedStats = null)
        {
            var gunObject = new GameObject("TestGun");
            _createdObjects.Add(gunObject);
            gunObject.transform.position = Vector3.zero;
            gunObject.transform.rotation = Quaternion.identity;

            var gun = gunObject.AddComponent<HitscanGun>();
            gun.Initialize(_definition, sharedStats);
            return gun;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing private method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, null);
        }

        private sealed class FakeDamageable : MonoBehaviour, IDamageable
        {
            public float CurrentHealth => 100f;
            public float MaxHealth => 100f;
            public bool IsDead => false;
            public int HitCount { get; private set; }
            public float LastDamage { get; private set; }
            public object LastSource { get; private set; }

            public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, object source = null)
            {
                HitCount++;
                LastDamage = amount;
                LastSource = source;
            }
        }
    }
}
