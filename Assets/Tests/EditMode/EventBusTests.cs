using NUnit.Framework;
using Roguelike.Core;

namespace Roguelike.Tests.EditMode
{
    public class EventBusTests
    {
        private readonly struct TestEvent
        {
            public readonly int Value;
            public TestEvent(int value) => Value = value;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<TestEvent>.Clear();
        }

        [Test]
        public void Raise_NotifiesSubscribedHandler()
        {
            int received = 0;
            EventBus<TestEvent>.Subscribe(e => received = e.Value);

            EventBus<TestEvent>.Raise(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_StopsReceivingEvents()
        {
            int callCount = 0;
            void Handler(TestEvent e) => callCount++;

            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Raise(new TestEvent(1));
            EventBus<TestEvent>.Unsubscribe(Handler);
            EventBus<TestEvent>.Raise(new TestEvent(2));

            Assert.AreEqual(1, callCount);
        }
    }
}
