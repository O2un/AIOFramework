using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using O2un.Roslyn.Analyzer;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;

namespace O2un.Pooling
{
    public sealed class PoolingManager : EngineSubsystemBase
    {
        private readonly Dictionary<string, IObjectPool> _pools = new();
        private Transform _globalPoolRoot;

        private readonly IObjectResolver _resolver;
        [Inject]
        public PoolingManager(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        protected override async UniTask InitAsync()
        {
            var rootGo = new GameObject("[Pool Manager Root]");
            Object.DontDestroyOnLoad(rootGo); // 싱글톤 스코프이므로 유지
            _globalPoolRoot = rootGo.transform;
            
            await UniTask.CompletedTask;
        }

        [CallBase]
        protected override void SafeDispose()
        {
            ClearAll();

            if (_globalPoolRoot != null)
            {
                Object.Destroy(_globalPoolRoot.gameObject);
            }
        }

        private void ClearAll()
        {
            foreach(var pool in _pools.Values)
            {
                pool.Dispose();
            }
            _pools.Clear();
        }

        public async UniTask<T> SpawnAsync<T,Tdata>(AssetReferenceT<T> assetRef, Tdata data) where T : SafeMono, IPoolObject<Tdata>
        {
            if(false == assetRef.RuntimeKeyIsValid())
            {
                Log.Print(Log.LogLevel.Error,"런타임 키가 존재 하지 않은 객체래퍼");
                return null;
            }
            var key = assetRef.RuntimeKey.ToString();
            return await SpawnAsync<T,Tdata>(key, data);
        }

        public async UniTask<T> SpawnAsync<T,Tdata>(string key, Tdata data) where T : SafeMono, IPoolObject<Tdata>
        {
            var pool = await GetOrCreatePool<T>(key);
            T instance = pool.Pop();

            _resolver.InjectGameObject(instance.gameObject);

            await instance.InitFromPool(data);
            instance.ID = key;

            return instance;
        }

        public void Despawn<T>(T obj) where T : SafeMono, IPoolObject
        {
            if(_pools.TryGetValue(obj.ID, out var pool))
            {
                var myPool = (ObjectPool<T>)pool;
                myPool.Push(obj);
            }
        }

        public async UniTask<ObjectPool<T>> GetOrCreatePool<T>(string key, int initialCount = 0, Transform parent = null) where T : Component
        {
            if (_pools.TryGetValue(key, out var poolObj))
            {
                return (ObjectPool<T>)poolObj;
            }
            
            GameObject go = new GameObject($"Pool_{key}");
            go.transform.SetParent(null != parent ? parent : _globalPoolRoot);
            
            var factory = new AddressablePoolFactory<T>(key, go.transform);
            var newPool = new ObjectPool<T>(factory, go.transform);
            
            await newPool.InitializeAsync(initialCount);

            _pools.Add(key, newPool);
            return newPool;
        }

        public void ClearPool(string key)
        {
            if (_pools.TryGetValue(key, out var poolObj))
            {
                poolObj.Dispose();
                _pools.Remove(key);
            }
        }
    }
}
