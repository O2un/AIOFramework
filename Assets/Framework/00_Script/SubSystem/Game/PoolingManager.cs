using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace O2un.Pooling
{
    public partial class PoolingManager : ServiceSubsystemBase
    {
        private readonly Dictionary<string, IObjectPool> _pools = new();
        private Transform _globalPoolRoot;
        protected override void Init()
        {
            var rootGo = new GameObject("[Pool Manager Root]");
            Object.DontDestroyOnLoad(rootGo);
            _globalPoolRoot = rootGo.transform;
        }
        
        public override void ClearAll()
        {
            foreach(var pool in _pools)
            {
                pool.Value.Dispose();
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
