using System;
using System.Collections.Generic;

namespace O2un.Core.Network
{
    public sealed class NetworkRouter
    {
        private readonly Dictionary<string, Delegate> _eventHandlers = new(StringComparer.Ordinal);

        public void Subscribe<T>(string eventName, Action<T> handler)
        {
            if (!_eventHandlers.ContainsKey(eventName))
                _eventHandlers[eventName] = handler;
            else
                _eventHandlers[eventName] = Delegate.Combine(_eventHandlers[eventName], handler);
        }

        public void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            if (_eventHandlers.TryGetValue(eventName, out var existingHandler))
            {
                var newHandler = Delegate.Remove(existingHandler, handler);
                if (newHandler == null)
                    _eventHandlers.Remove(eventName);
                else
                    _eventHandlers[eventName] = newHandler;
            }
        }

        public void Route<T>(string eventName, T data)
        {
            if (_eventHandlers.TryGetValue(eventName, out var handler) && handler is Action<T> action)
            {
                action.Invoke(data);
            }
        }

        public void Clear()
        {
            _eventHandlers.Clear();
        }
    }
}