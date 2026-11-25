using NUnit.Framework;
using Roguelike.Core;
using Roguelike.Core.Events;
using UnityEngine;

namespace Roguelike.Tests.EditMode
{
    public class CoreContractsTests
    {
        private class StubDifficultyProvider : IDifficultyProvider
        {
            public float Current { get; set; }
        }

        private class StubDamageable : IDamageable
        {
            public float CurrentHealth { get; private set; }
            public float MaxHealth { get; }
            public bool IsDead => CurrentHealth <= 0f;
            public Vector3 LastHitPoint { get; private set; }
            public object LastSource { get; private set; }

            public StubDamageable(float maxHealth)
            {
                MaxHealth = maxHealth;
                CurrentHealth = maxHealth;
            }

            public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, object source = null)
            {
                CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
                LastHitPoint = hitPoint;
                LastSource = source;
            }
        }

        private class StubWeapon : IWeapon
        {
            public int CurrentAmmo { get; private set; }
            public int MaxAmmo { get; }
            public bool IsReloading { get; private set; }

            public StubWeapon(int maxAmmo)
            {
                MaxAmmo = maxAmmo;
                CurrentAmmo = maxAmmo;
            }

            public void TryFire()
            {
                if (!IsReloading && CurrentAmmo > 0)
                {
                    CurrentAmmo--;
                }
            }

            public void Reload()
            {
                IsReloading = false;
                CurrentAmmo = MaxAmmo;
            }
        }

        [TearDown]
        public void TearDown()
        {
            Services.Clear();
        }

        [Test]
        public void DifficultyProvider_ResolvesThroughServices()
        {
            var provider = new StubDifficultyProvider { Current = 2.75f };
            Services.Register<IDifficultyProvider>(provider);

            Assert.AreEqual(2.75f, Services.Get<IDifficultyProvider>().Current, 0.001f);
        }

        [Test]
        public void Damageable_AppliesDamageAndTracksDeathState()
        {
            IDamageable target = new StubDamageable(50f);
            var hitPoint = new Vector3(1f, 2f, 3f);
            var attacker = new object();

            target.TakeDamage(20f, hitPoint, Vector3.up, attacker);
            Assert.AreEqual(30f, target.CurrentHealth, 0.001f);
            Assert.IsFalse(target.IsDead);

            target.TakeDamage(40f, hitPoint, Vector3.up, attacker);
            Assert.AreEqual(0f, target.CurrentHealth, 0.001f);
            Assert.IsTrue(target.IsDead);
        }

        [Test]
        public void Weapon_FiresAndReloadsThroughContract()
        {
            IWeapon weapon = new StubWeapon(2);

            weapon.TryFire();
            Assert.AreEqual(1, weapon.CurrentAmmo);

            weapon.TryFire();
            Assert.AreEqual(0, weapon.CurrentAmmo);

            weapon.Reload();
            Assert.AreEqual(2, weapon.CurrentAmmo);
            Assert.IsFalse(weapon.IsReloading);
        }

        [Test]
        public void RunLifecycleEvents_RaiseWithExpectedPayloads()
        {
            int totalRaised = 0;
            EnemyKilled receivedKill = default;
            StageStarted receivedStage = default;
            RoomEntered receivedRoomEnter = default;
            RoomCleared receivedRoomClear = default;
            RunEnded receivedRunEnd = default;

            void OnRunStarted(RunStarted _) => totalRaised++;
            void OnStageStarted(StageStarted e) { totalRaised++; receivedStage = e; }
            void OnRoomEntered(RoomEntered e) { totalRaised++; receivedRoomEnter = e; }
            void OnRoomCleared(RoomCleared e) { totalRaised++; receivedRoomClear = e; }
            void OnShopEntered(ShopEntered _) => totalRaised++;
            void OnShopExited(ShopExited _) => totalRaised++;
            void OnPlayerDied(PlayerDied _) => totalRaised++;
            void OnRunEnded(RunEnded e) { totalRaised++; receivedRunEnd = e; }
            void OnEnemyKilled(EnemyKilled e) { totalRaised++; receivedKill = e; }

            EventBus<RunStarted>.Subscribe(OnRunStarted);
            EventBus<StageStarted>.Subscribe(OnStageStarted);
            EventBus<RoomEntered>.Subscribe(OnRoomEntered);
            EventBus<RoomCleared>.Subscribe(OnRoomCleared);
            EventBus<ShopEntered>.Subscribe(OnShopEntered);
            EventBus<ShopExited>.Subscribe(OnShopExited);
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<RunEnded>.Subscribe(OnRunEnded);
            EventBus<EnemyKilled>.Subscribe(OnEnemyKilled);

            try
            {
                var killPos = new Vector3(4f, 0f, -2f);
                EventBus<RunStarted>.Raise(new RunStarted());
                EventBus<StageStarted>.Raise(new StageStarted(1));
                EventBus<RoomEntered>.Raise(new RoomEntered(3));
                EventBus<EnemyKilled>.Raise(new EnemyKilled(killPos, EnemyTier.Elite));
                EventBus<RoomCleared>.Raise(new RoomCleared(3));
                EventBus<ShopEntered>.Raise(new ShopEntered());
                EventBus<ShopExited>.Raise(new ShopExited());
                EventBus<PlayerDied>.Raise(new PlayerDied());
                EventBus<RunEnded>.Raise(new RunEnded(false));

                Assert.AreEqual(9, totalRaised);
                Assert.AreEqual(1, receivedStage.StageIndex);
                Assert.AreEqual(3, receivedRoomEnter.RoomIndex);
                Assert.AreEqual(3, receivedRoomClear.RoomIndex);
                Assert.AreEqual(killPos, receivedKill.Position);
                Assert.AreEqual(EnemyTier.Elite, receivedKill.Tier);
                Assert.IsFalse(receivedRunEnd.Victory);
            }
            finally
            {
                EventBus<RunStarted>.Unsubscribe(OnRunStarted);
                EventBus<StageStarted>.Unsubscribe(OnStageStarted);
                EventBus<RoomEntered>.Unsubscribe(OnRoomEntered);
                EventBus<RoomCleared>.Unsubscribe(OnRoomCleared);
                EventBus<ShopEntered>.Unsubscribe(OnShopEntered);
                EventBus<ShopExited>.Unsubscribe(OnShopExited);
                EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
                EventBus<RunEnded>.Unsubscribe(OnRunEnded);
                EventBus<EnemyKilled>.Unsubscribe(OnEnemyKilled);
            }
        }
    }
}
