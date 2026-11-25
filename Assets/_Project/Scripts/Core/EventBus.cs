using System;

// Acts as a global event bus for events of type T. It allows subscribing, unsubscribing, raising events, and clearing all handlers.
namespace Roguelike.Core
{
    public static class EventBus<T> where T : struct
    {
        private static event Action<T> _handlers;

        public static void Subscribe(Action<T> handler)
        {
            _handlers += handler;
        }

        public static void Unsubscribe(Action<T> handler)
        {
            _handlers -= handler;
        }

        public static void Raise(T eventData)
        {
            _handlers?.Invoke(eventData);
        }

        public static void Clear()
        {
            _handlers = null;
        }
    }
}
