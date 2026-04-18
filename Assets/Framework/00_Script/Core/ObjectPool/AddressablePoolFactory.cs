using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using UnityEngine;

namespace O2un.Pooling
{
    public class AddressablePoolFactory<T> : IPoolFactory<T> where T : Component
    {
        private readonly string _addressableKey;
        private readonly Transform _parentRoot;
        private GameObject _cachedPrefab;

        public AddressablePoolFactory(string key, Transform parent = null)
        {
            _addressableKey = key;
            _parentRoot = parent;   
        }

        public async UniTask PreloadAsync()
        {
            if (_cachedPrefab != null) return;

            _cachedPrefab = await AddressablesUtils.LoadAssetAsync<GameObject>(_addressableKey);
            
            if (_cachedPrefab == null)
            {
                Log.Print(Log.LogLevel.Error, $"프리팹 로드 실패 : {_addressableKey}");
            }
        }

        public T Create()
        {
            if (_cachedPrefab == null)
            {
                Log.Dev($"{_addressableKey} 원본이 없습니다. PreloadAsync를 먼저 호출하세요.", Log.LogLevel.Error);
                return null;
            }

            var instance = Object.Instantiate(_cachedPrefab, _parentRoot);
            var component = instance.GetComponent<T>();
            
            if (component == null)
            {
                Log.Dev($"{_addressableKey} 원본이 없습니다. PreloadAsync를 먼저 호출하세요.", Log.LogLevel.Error);
            }
            
            return component;
        }

        public void Release()
        {
            if (_cachedPrefab != null)
            {
                AddressablesUtils.ReleaseAsset(_addressableKey);
                _cachedPrefab = null;
            }
        }
    }
}
