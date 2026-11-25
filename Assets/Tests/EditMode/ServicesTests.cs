using System;
using NUnit.Framework;
using Roguelike.Core;

namespace Roguelike.Tests.EditMode
{
    public class ServicesTests
    {
        private interface IDummyService { int GetNumber(); }
        private class DummyService : IDummyService { public int GetNumber() => 99; }

        [TearDown]
        public void TearDown()
        {
            Services.Clear();
        }

        [Test]
        public void Get_ReturnsRegisteredService()
        {
            var dummy = new DummyService();
            Services.Register<IDummyService>(dummy);

            var resolved = Services.Get<IDummyService>();

            Assert.AreSame(dummy, resolved);
            Assert.AreEqual(99, resolved.GetNumber());
        }

        [Test]
        public void Get_ThrowsWhenServiceNotRegistered()
        {
            Assert.Throws<InvalidOperationException>(() => Services.Get<IDummyService>());
        }
    }
}
