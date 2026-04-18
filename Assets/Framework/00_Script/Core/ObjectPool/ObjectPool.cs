using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace O2un.Pooling
{
    public interface IObjectPool
    {
        public void Dispose();
    }
    public class ObjectPool<T> : IObjectPool where T : Component
    {
        private readonly IPoolFactory<T> _factory;
        private readonly Stack<T> _pool = new();
        private readonly Transform _poolRoot;

        public ObjectPool(IPoolFactory<T> factory, Transform poolRoot)
        {
            _factory = factory;
            _poolRoot = poolRoot;
        }

        public async UniTask InitializeAsync(int initialCapacity = 0)
        {
            await _factory.PreloadAsync();
            
            for (int i = 0; i < initialCapacity; i++)
            {
                var instance = _factory.Create();
                if (instance != null)
                {
                    instance.gameObject.SetActive(false);
                    _pool.Push(instance);
                }
            }
        }

        public T Pop()
        {
            T instance;

            if (_pool.TryPop(out instance))
            {
                instance.gameObject.SetActive(true);
                return instance;
            }
            
            instance = _factory.Create();
            if (instance != null)
            {
                instance.gameObject.SetActive(true);
            }
            
            return instance;
        }

        public void Push(T instance)
        {
            if (instance == null) return;

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_poolRoot);
            _pool.Push(instance);
        }

        public void Dispose()
        {
            while (_pool.TryPop(out var instance))
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
            _factory.Release();
        }
    }
}
