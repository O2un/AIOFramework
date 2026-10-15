using Cysharp.Threading.Tasks;
using O2un.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace O2un.Pooling
{
    public interface IPoolService : IAsyncReady
    {
        UniTask<ObjectPool<T>> GetOrCreatePool<T>(string key, int initialCount = 0, Transform parent = null) where T : Component;
        void ClearPool(string key);
        UniTask<T> SpawnAsync<T, TData>(string key, TData data) where T : SafeMono, IPoolObject<TData>;
        UniTask<T> SpawnAsync<T, TData>(AssetReferenceT<T> assetRef, TData data) where T : SafeMono, IPoolObject<TData>;
        void Despawn<T>(T obj) where T : SafeMono, IPoolObject;
    }
}
