using System;
using System.Collections.Generic;
using O2un.Core;
using O2un.Reactive;
using R3;

namespace O2un.Security
{
    /// <summary>
    /// 암호화 값(Obscured*)을 드는 DataStore 의 부모. 겉모양은 ReactiveClass 와 같고, Select 가 복호화된 직전 값을 보관하지 않는다.
    /// </summary>
    public abstract class ObscuredStoreClass<TState> : SafeDisposableClass, IReadOnlyReactiveClass<TState>, IWriteReactiveClass<TState>
    {
        private readonly ReactiveProperty<TState> _state;

        protected ObscuredStoreClass(TState initialState)
        {
            _state = new ReactiveProperty<TState>(initialState);
        }

        public ReadOnlyReactiveProperty<TState> State => _state;
        public TState Current => _state.Value;

        protected override void SafeDispose()
        {
            _state.Dispose();

            base.SafeDispose();
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
            // 값 단계에서 DistinctUntilChanged 하면 직전 평문이 남는다 — 암호화된 상태 단계에서 거른 뒤 꺼낸다.
            return _state.DistinctUntilChanged(new SelectedEquality<TValue>(selector)).Select(selector);
        }

        private sealed class SelectedEquality<TValue> : IEqualityComparer<TState>
        {
            private readonly Func<TState, TValue> _selector;

            public SelectedEquality(Func<TState, TValue> selector)
            {
                _selector = selector;
            }

            public bool Equals(TState x, TState y)
            {
                return EqualityComparer<TValue>.Default.Equals(_selector(x), _selector(y));
            }

            public int GetHashCode(TState obj)
            {
                return EqualityComparer<TValue>.Default.GetHashCode(_selector(obj));
            }
        }
    }
}
