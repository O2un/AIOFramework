using System;
using System.Collections.Generic;
using O2un.Core;
using R3;
using UnityEngine;

namespace O2un.Reactive
{
    public interface IUISwitchReader<TKey>
    {
        Observable<bool> Observe(TKey key);
        bool Get(TKey key);
    }
    public interface IUISwitchWriter<TKey>
    {
        void Set(TKey key, bool value);
        void Show(TKey key);
        void Hide(TKey key);
        void Toggle(TKey key);
    }

    public abstract class UISwitchStore<TKey> : SafeDisposableClass where TKey : Enum
    {
        private readonly Dictionary<TKey, ReactiveProperty<bool>> _states = new();

        public Observable<bool> Observe(TKey key)
        {
            return GetProperty(key);
        }

        public bool Get(TKey key)
        {
            return GetProperty(key).Value;
        }

        public void Set(TKey key, bool value)
        {
            GetProperty(key).Value = value;
        }

        public void Show(TKey key)
        {
            Set(key, true);
        }

        public void Hide(TKey key)
        {
            Set(key, false);
        }

        public void Toggle(TKey key)
        {
            Set(key, !Get(key));
        }

        private ReactiveProperty<bool> GetProperty(TKey key)
        {
            if (_states.TryGetValue(key, out var property))
                return property;

            property = new ReactiveProperty<bool>(false);
            _states.Add(key, property);
            return property;
        }

        protected override void SafeDispose()
        {
            foreach (var property in _states.Values)
            {
                property.Dispose();
            }

            _states.Clear();
        }
    }
}
