

using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace O2un.Core.Utils
{
    public static class AddressablesUtils
    {
        private class CacheData
        {
            public AsyncOperationHandle _handle;
            public int RefCount;
    
            public bool IsDone => _handle.IsDone;
            public object Result => _handle.Result;
            public object GetCache()
            {
                ++RefCount;
                return Result;
            }
            public UniTask ToUniTask => _handle.ToUniTask();
            public bool TryRelease()
            {
                --RefCount;
                if(RefCount <= 0)
                {
                    Addressables.Release(_handle);
                    return true;
                }
    
                return false;
            }
        }
    
        private static readonly Dictionary<string, CacheData> _handleCache = new();
    
        public static async UniTask<T> LoadAssetAsync<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AddressablesUtils] Key가 비어있습니다.");
                return null;
            }
    
            if (_handleCache.TryGetValue(key, out var cachedHandle))
            {
                if (cachedHandle.IsDone)
                {
                    return cachedHandle.GetCache() as T;
                }
                    
                await cachedHandle.ToUniTask;
                return cachedHandle.GetCache() as T;
            }
    
            var handle = Addressables.LoadAssetAsync<T>(key);
            _handleCache.Add(key, new(){ _handle = handle, RefCount = 1});
    
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return handle.WaitForCompletion();
            }
    #endif
    
            await handle.ToUniTask();
    
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }
            else
            {
                Debug.LogError($"[AddressableHelper] 에셋 로드 실패: {key}");
                _handleCache.Remove(key);
                Addressables.Release(handle);
                return null;
            }
        }
    
        public static async UniTask<IList<T>> LoadAssetsAsync<T>(string key, System.Action<T> onEachLoaded = null) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AddressablesUtils] Key가 비어있습니다.");
                return null;
            }
            
            string listKey = $"KEYLIST_{key}";
            if (_handleCache.TryGetValue(listKey, out var cachedHandle))
            {
                if (cachedHandle.IsDone)
                {                
                    return cachedHandle.GetCache() as IList<T>;
                }
                    
                await cachedHandle.ToUniTask;
                return cachedHandle.GetCache() as IList<T>;
            }
    
            var handle = Addressables.LoadAssetsAsync<T>(key, onEachLoaded);
            _handleCache.Add(listKey, new(){ _handle = handle, RefCount = 1});
    
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return handle.WaitForCompletion();
            }
    #endif
    
            await handle.ToUniTask();
    
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }
            else
            {
                Debug.LogError($"[AddressableHelper] 에셋 로드 실패: {key}");
                _handleCache.Remove(listKey);
                Addressables.Release(handle);
                return null;
            }
        }
    
        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : Object
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Debug.LogError("[AddressableHelper] 유효하지 않은 AssetReference입니다.");
                return null;
            }
            
            string key = reference.RuntimeKey.ToString();
            return await LoadAssetAsync<T>(key);
        }
    
        public static async UniTask<IList<T>> LoadAssetsAsync<T>(AssetLabelReference labelReference, System.Action<T> onEachLoaded = null) where T : Object
        {
            if (labelReference == null || string.IsNullOrEmpty(labelReference.labelString))
            {
                Debug.LogError("[AddressableHelper] 유효하지 않은 AssetLabelReference입니다.");
                return null;
            }
    
            return await LoadAssetsAsync<T>(labelReference.labelString, onEachLoaded);
        }
    
        public static void ReleaseAsset(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
    
            if (_handleCache.TryGetValue(key, out var handle))
            {
                if(handle.TryRelease())
                {
                    _handleCache.Remove(key);
                }
            }
        }
        
        public static void ReleaseAssets(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            
            string listKey = $"KEYLIST_{key}";
            if (_handleCache.TryGetValue(listKey, out var handle))
            {
                if(handle.TryRelease())
                {
                    _handleCache.Remove(listKey);
                }
            }
        }
    
        public static void ReleaseAsset(AssetReference reference)
        {
            if (reference != null && reference.RuntimeKeyIsValid())
            {
                ReleaseAsset(reference.RuntimeKey.ToString());
            }
        }
    
        public static void ReleaseAssets(AssetLabelReference labelReference)
        {
            if (labelReference != null && !string.IsNullOrEmpty(labelReference.labelString))
            {
                ReleaseAssets(labelReference.labelString);
            }
        }
    
        public static void ReleaseAll()
        {
            foreach (var handle in _handleCache.Values)
            {
                Addressables.Release(handle._handle);
            }
            _handleCache.Clear();
        }
    }
}
