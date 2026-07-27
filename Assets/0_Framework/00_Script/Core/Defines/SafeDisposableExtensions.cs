using System;
using R3;
using UnityEngine.Events;

namespace O2un.Core
{
    /// <summary>
    /// 네이티브 이벤트의 등록/해제 쌍을 DisposableR3 수명에 묶는다.
    /// UnityEvent(UGUI), C# event, 임의의 subscribe/unsubscribe 쌍이 대상이다.
    /// </summary>
    public static class SafeDisposableExtensions
    {
        public static T Track<T, TState>(this ISafeDisposable owner, T element, TState state, Action<T, TState> subscribe, Action<T, TState> unsubscribe)
        {
            if (null == owner)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (owner.IsDisposed)
            {
                throw new ObjectDisposedException(owner.GetType().Name);
            }

            if (null == subscribe)
            {
                throw new ArgumentNullException(nameof(subscribe));
            }

            if (null == unsubscribe)
            {
                throw new ArgumentNullException(nameof(unsubscribe));
            }

            subscribe(element, state);

            try
            {
                Disposable.Create(
                    new TrackedRegistration<T, TState>(element, state, unsubscribe),
                    static registration => registration.Unsubscribe()).AddTo(owner.DisposableR3);
            }
            catch
            {
                // 수명 등록에 실패하면 해제할 주체가 없으므로 즉시 되돌린다
                unsubscribe(element, state);
                throw;
            }

            return element;
        }

        public static UnityEvent AddListener(this ISafeDisposable owner, UnityEvent unityEvent, UnityAction listener)
        {
            if (null == unityEvent)
            {
                throw new ArgumentNullException(nameof(unityEvent));
            }

            if (null == listener)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            return owner.Track(
                unityEvent,
                listener,
                static (targetEvent, targetListener) => targetEvent.AddListener(targetListener),
                static (targetEvent, targetListener) => targetEvent.RemoveListener(targetListener));
        }

        public static UnityEvent<T> AddListener<T>(this ISafeDisposable owner, UnityEvent<T> unityEvent, UnityAction<T> listener)
        {
            if (null == unityEvent)
            {
                throw new ArgumentNullException(nameof(unityEvent));
            }

            if (null == listener)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            return owner.Track(
                unityEvent,
                listener,
                static (targetEvent, targetListener) => targetEvent.AddListener(targetListener),
                static (targetEvent, targetListener) => targetEvent.RemoveListener(targetListener));
        }

        public static EventBinding<TDelegate> TrackEvent<TDelegate>(this ISafeDisposable owner, EventBinding<TDelegate> binding, TDelegate listener)
        {
            if (binding.IsEmpty)
            {
                throw new ArgumentException("Event binding is empty.", nameof(binding));
            }

            if (listener is null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            return owner.Track(
                binding,
                listener,
                static (targetBinding, targetListener) => targetBinding.Subscribe(targetListener),
                static (targetBinding, targetListener) => targetBinding.Unsubscribe(targetListener));
        }

        public static EventBinding<TDelegate> AddTo<TDelegate>(this EventBinding<TDelegate> binding, ISafeDisposable owner, TDelegate listener)
        {
            return owner.TrackEvent(binding, listener);
        }

        private readonly struct TrackedRegistration<T, TState>
        {
            private readonly T _element;
            private readonly TState _state;
            private readonly Action<T, TState> _unsubscribe;

            public TrackedRegistration(T element, TState state, Action<T, TState> unsubscribe)
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
    }

    /// <summary>
    /// C# event의 += / -= 쌍을 값으로 포장한다. event는 인자로 넘길 수 없기 때문에 람다 두 개로 감싼다.
    /// </summary>
    public readonly struct EventBinding<TDelegate>
    {
        private readonly Action<TDelegate> _subscribe;
        private readonly Action<TDelegate> _unsubscribe;

        public bool IsEmpty => null == _subscribe || null == _unsubscribe;

        public EventBinding(Action<TDelegate> subscribe, Action<TDelegate> unsubscribe)
        {
            _subscribe = subscribe ?? throw new ArgumentNullException(nameof(subscribe));
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        }

        public void Subscribe(TDelegate listener)
        {
            if (listener is null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            _subscribe(listener);
        }

        public void Unsubscribe(TDelegate listener)
        {
            if (listener is null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            _unsubscribe(listener);
        }
    }

    public static class EventBinding
    {
        public static EventBinding<Action> Create(Action<Action> subscribe, Action<Action> unsubscribe)
        {
            return new EventBinding<Action>(subscribe, unsubscribe);
        }

        public static EventBinding<Action<T>> Create<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe)
        {
            return new EventBinding<Action<T>>(subscribe, unsubscribe);
        }

        public static EventBinding<Action<T1, T2>> Create<T1, T2>(Action<Action<T1, T2>> subscribe, Action<Action<T1, T2>> unsubscribe)
        {
            return new EventBinding<Action<T1, T2>>(subscribe, unsubscribe);
        }

        public static EventBinding<TDelegate> Create<TDelegate>(Action<TDelegate> subscribe, Action<TDelegate> unsubscribe)
        {
            return new EventBinding<TDelegate>(subscribe, unsubscribe);
        }
    }
}
