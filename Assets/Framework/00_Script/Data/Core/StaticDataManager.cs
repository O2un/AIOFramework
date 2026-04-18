using System.Collections.Generic;
using O2un.Core;
using O2un.Core.Utils;
using O2un.Utils;

namespace O2un.Data 
{
    public interface IStaticDataManager
    {
        void Set();
        void Link();
    }

    public abstract class StaticDataManager<T> : IStaticDataManager where T : StaticData, new()
    {
        protected readonly Dictionary<long, T> _dataDict = new();

        public T Get(UniqueKey key)
        {
            return _dataDict.GetValueOrDefault(key.Raw);
        }
        
        public void Set()
        {
            foreach(var data in _dataDict.Values)
            {
                if(false == data.Set())
                {
                    Log.Print(Log.LogLevel.Error, $"데이터 Set 실패 {data.Key}");
                    CommonUtils.Quit();
                }
            }
        }

        public void Link()
        {
            foreach(var data in _dataDict.Values)
            {
                if(false == data.Link())
                {
                    Log.Print(Log.LogLevel.Error, $"데이터 Link 실패 {data.Key}");
                    CommonUtils.Quit();
                }
            }
        }
    }
}