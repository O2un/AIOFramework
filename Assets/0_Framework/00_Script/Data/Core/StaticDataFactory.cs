using System;
using System.Collections.Generic;
using O2un.Core;

namespace O2un.Data
{
    internal static class ManagerCache<TData> where TData : StaticData, new()
    {
        public static StaticDataManager<TData> Instance;
    }

    public class StaticDataFactory
    {
        private static readonly Dictionary<Type, IStaticDataManager> _registered = new();

        /// <summary>
        /// 등록을 관찰만 하는 자리다. 매니저를 살려두지 않고, 진단 창이 Play 중 실제로
        /// 등록된 것을 그대로 보여주기 위해서만 있다. 도메인 리로드와 함께 비워진다.
        /// </summary>
        public static IReadOnlyDictionary<Type, IStaticDataManager> Registered => _registered;

        public void Register<TData>(StaticDataManager<TData> manager) where TData : StaticData, new()
        {
            ManagerCache<TData>.Instance = manager;
            _registered[typeof(TData)] = manager;
        }

        public TManager GetManager<TManager, TData>() 
            where TManager : StaticDataManager<TData> 
            where TData : StaticData, new()
        {
            return ManagerCache<TData>.Instance as TManager;
        }

        public TData GetData<TData>(UniqueKey key) where TData : StaticData, new()
        {
            return ManagerCache<TData>.Instance?.Get(key);
        }
    }
}