using System;
using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;

namespace O2un.Core
{
    /// <summary>
    /// Owns callback registrations associated with one VisualElement.
    /// Disposing this object unregisters only callbacks registered through this binding;
    /// it does not remove or destroy the VisualElement itself.
    /// </summary>
    public sealed class VisualElementBinding<TElement> : IDisposable where TElement : VisualElement
    {
        private DisposableBag _registrations;
        private bool _isDisposed;

        public TElement Element { get; }
        public bool IsDisposed => _isDisposed;

        internal VisualElementBinding(TElement element)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        /// <summary>
        /// Assigns the resolved VisualElement to a field while keeping the binding chain fluent.
        /// </summary>
        public VisualElementBinding<TElement> Assign(out TElement element)
        {
            ThrowIfDisposed();
            element = Element;
            return this;
        }

        /// <summary>
        /// Registers a UI Toolkit event callback and records the matching unregister operation.
        /// </summary>
        public VisualElementBinding<TElement> On<TEvent>(EventCallback<TEvent> callback, TrickleDown trickleDown = TrickleDown.NoTrickleDown) where TEvent : EventBase<TEvent>, new()
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Track(
                new EventRegistration<TEvent>(callback, trickleDown),
                static (element, registration) =>
                    element.RegisterCallback(
                        registration.Callback,
                        registration.TrickleDown),
                static (element, registration) =>
                    element.UnregisterCallback(
                        registration.Callback,
                        registration.TrickleDown));
        }

        /// <summary>
        /// Registers a UI Toolkit event callback with user arguments and records its unregister operation.
        /// </summary>
        public VisualElementBinding<TElement> On<TEvent, TUserArgs>(EventCallback<TEvent, TUserArgs> callback, TUserArgs userArgs, TrickleDown trickleDown = TrickleDown.NoTrickleDown) where TEvent : EventBase<TEvent>, new()
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Track(
                new EventRegistration<TEvent, TUserArgs>(callback, userArgs, trickleDown),
                static (element, registration) =>
                    element.RegisterCallback(
                        registration.Callback,
                        registration.UserArgs,
                        registration.TrickleDown),
                static (element, registration) =>
                    element.UnregisterCallback(
                        registration.Callback,
                        registration.TrickleDown));
        }

        /// <summary>
        /// Tracks an arbitrary subscribe/unsubscribe pair against this element.
        /// </summary>
        public VisualElementBinding<TElement> Track<TState>(TState state, Action<TElement, TState> subscribe, Action<TElement, TState> unsubscribe)
        {
            ThrowIfDisposed();

            if (subscribe == null)
            {
                throw new ArgumentNullException(nameof(subscribe));
            }

            if (unsubscribe == null)
            {
                throw new ArgumentNullException(nameof(unsubscribe));
            }

            subscribe(Element, state);

            try
            {
                Disposable.Create(new TrackedRegistration<TState>(Element, state, unsubscribe), static registration => registration.Unsubscribe()).AddTo(ref _registrations);
            }
            catch
            {
                // Do not leave a callback registered if lifetime tracking itself fails.
                unsubscribe(Element, state);
                throw;
            }

            return this;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _registrations.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private readonly struct EventRegistration<TEvent> where TEvent : EventBase<TEvent>, new()
        {
            public EventCallback<TEvent> Callback { get; }
            public TrickleDown TrickleDown { get; }

            public EventRegistration(EventCallback<TEvent> callback, TrickleDown trickleDown)
            {
                Callback = callback;
                TrickleDown = trickleDown;
            }
        }

        private readonly struct EventRegistration<TEvent, TUserArgs> where TEvent : EventBase<TEvent>, new()
        {
            public EventCallback<TEvent, TUserArgs> Callback { get; }
            public TUserArgs UserArgs { get; }
            public TrickleDown TrickleDown { get; }

            public EventRegistration(EventCallback<TEvent, TUserArgs> callback, TUserArgs userArgs, TrickleDown trickleDown)
            {
                Callback = callback;
                UserArgs = userArgs;
                TrickleDown = trickleDown;
            }
        }

        private readonly struct TrackedRegistration<TState>
        {
            private readonly TElement _element;
            private readonly TState _state;
            private readonly Action<TElement, TState> _unsubscribe;

            public TrackedRegistration(TElement element, TState state, Action<TElement, TState> unsubscribe)
            {
                _element = element;
                _state = state;
                _unsubscribe = unsubscribe;
            }

            public void Unsubscribe()
            {
                _unsubscribe(_element, _state);
            }
        }

        public VisualElementBinding<TElement> ValueChanged<TValue>(EventCallback<ChangeEvent<TValue>> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Track( callback,
                static (element, handler) =>
                {
                    ((INotifyValueChanged<TValue>)element).RegisterValueChangedCallback(handler);
                },
                static (element, handler) =>
                {
                    ((INotifyValueChanged<TValue>)element).UnregisterValueChangedCallback(handler);
                });
        }

        public VisualElementBinding<TElement> ValueChangedValue<TValue>(Action<TValue> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            EventCallback<ChangeEvent<TValue>> handler = evt => callback(evt.newValue);
            return ValueChanged(handler);
        }

        public VisualElementBinding<TElement> RegisterCallback<TEventType>(EventCallback<TEventType> callback) where TEventType : EventBase<TEventType>, new()
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Track( callback,
                static (element, handler) =>
                {
                    element.RegisterCallback(handler);
                },
                static (element, handler) =>
                {
                    element.UnregisterCallback(handler);
                });
        }
    }

    public static class VisualElementBindingExtensions
    {
        /// <summary>
        /// Uses Button.clicked rather than ClickEvent so keyboard/gamepad submit is retained.
        /// </summary>
        public static VisualElementBinding<Button> Clicked(this VisualElementBinding<Button> binding, Action callback)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return binding.Track(
                callback,
                static (button, handler) => button.clicked += handler,
                static (button, handler) => button.clicked -= handler);
        }

        public static VisualElementBinding<ListView> SelectChanged(this VisualElementBinding<ListView> binding, Action<IEnumerable<object>> callback)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return binding.Track(
                callback,
                static (button, handler) => button.selectionChanged += handler,
                static (button, handler) => button.selectionChanged -= handler);
        }
    }
}
