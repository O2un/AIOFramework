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
        public void Register<TData>(StaticDataManager<TData> manager) where TData : StaticData, new()
        {
            ManagerCache<TData>.Instance = manager;
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