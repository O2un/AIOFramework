using System;
using O2un.Core;
using R3;

namespace O2un.Reactive
{
    public interface IReadOnlyReactiveClass<TState>
    {
        ReadOnlyReactiveProperty<TState> State {get;}
        TState Current {get;}
        Observable<TValue> Select<TValue>(Func<TState,TValue> selector);
    }

    public interface IWriteReactiveClass<TState>
    {
        void Set(TState state);
        void Update(Func<TState, TState> updater);
    }

    public abstract class ReactiveClass<TState> : SafeDisposableClass, IReadOnlyReactiveClass<TState>, IWriteReactiveClass<TState>
    {
        private readonly ReactiveProperty<TState> _state;
        public ReadOnlyReactiveProperty<TState> State => _state;
        public TState Current => _state.Value;

        public ReactiveClass(TState initialState)
        {
            _state = new ReactiveProperty<TState>(initialState);
        }

        public void Set(TState state)
        {
            _state.Value = state;
        }

        public void Update(Func<TState, TState> updater)
        {
            _state.Value = updater(_state.Value);
        }

        public Observable<TValue> Select<TValue>(Func<TState, TValue> selector)
        {
            return _state.Select(selector).DistinctUntilChanged();
        }

        protected override void SafeDispose()
        {
            _state.Dispose();
        }
    }
}