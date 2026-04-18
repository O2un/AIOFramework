using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace O2un.Pooling
{
    public static class ObjectPoolUtils
    {
        private static PoolingManager _manager;
        private static PoolingManager Manager => _manager ??= SystemProvider.GetSubsystem<PoolingManager>();

        public static async UniTask<T> Spawn<T, TData>(this SafeMono mono, string key, TData data) where T : SafeMono, IPoolObject<TData>
        {
            return await Manager.SpawnAsync<T,TData>(key,data);
        }
        
        public static async UniTask<T> Spawn<T, TData>(this SafeMono mono, AssetReferenceT<T> refT, TData data) where T : SafeMono, IPoolObject<TData>
        {
            return await Manager.SpawnAsync(refT,data);
        }

        public static void Despawn<T>(this T mono) where T : SafeMono, IPoolObject
        {
            Manager?.Despawn(mono);
        }
    }
}