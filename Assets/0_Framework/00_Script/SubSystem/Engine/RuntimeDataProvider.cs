using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;

namespace O2un.Core.Data
{
    /// <summary>
    /// 런타임 데이터를 타입당 하나만 만들어 캐시한다. 같은 JSON 파일을 여러 인스턴스가 따로 들고 서로 덮어쓰는 것을 막는다.
    /// </summary>
    public sealed class RuntimeDataProvider : EngineSubsystemBase, IRuntimeDataProvider
    {
        private readonly Dictionary<Type, IRuntimeData> _dataMap = new();
        private readonly Dictionary<string, Type> _keyOwners = new();

        protected override UniTask InitAsync() => UniTask.CompletedTask;

        public T Get<T>() where T : RuntimeData<T>, new()
        {
            Type type = typeof(T);

            if(true == _dataMap.TryGetValue(type, out IRuntimeData cached))
            {
                return (T)cached;
            }

            T data = new();

            // Key 가 겹치면 두 타입이 한 파일을 번갈아 덮어써 데이터가 조용히 사라진다.
            if(true == _keyOwners.TryGetValue(data.Key, out Type owner))
            {
                Log.Print(Log.LogLevel.Error, $"런타임 데이터 Key 중복: '{data.Key}' 를 {owner.Name} 와 {type.Name} 가 함께 쓴다.");
            }
            else
            {
                _keyOwners.Add(data.Key, type);
            }

            data.Load();
            _dataMap.Add(type, data);

            return data;
        }

        protected override void SafeDispose()
        {
            _dataMap.Clear();
            _keyOwners.Clear();
        }
    }
}
