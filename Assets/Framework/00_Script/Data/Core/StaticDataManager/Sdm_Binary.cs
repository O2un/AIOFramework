using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using O2un.Core;
using O2un.Data.Binary;
using UnityEngine;

namespace O2un.Data 
{
    public abstract partial class StaticDataManager<T> : IStaticDataManager where T : StaticData, new()
    {
        public void SaveToBinary()
        {
#if UNITY_EDITOR
            var config = EditorHelper.GetOrCreateSettings<StaticDataConfig>(StaticDataConfig.GLOBALCONFIGPATH);
            using var bw = BinaryHelper.SaveToBinary(config.BINARYPATH + typeof(T).Name + config.BINARYSUFFIX);
            bw.Write(DataList.Count);
            foreach (var d in DataList)
            {
                bw.Write(d.Key.Raw);
                WriteToBinary(bw, d.Value);
            }
#endif
        }

        protected abstract void WriteToBinary(BinaryWriter bw, T data);
        protected abstract T ReadFromBinary(BinaryReader br, UniqueKey key);

        protected void LoadFromBinary()
        {
            var config = Resources.Load<StaticDataConfig>(StaticDataConfig.GLOBALCONFIGPATH);

            using var br = BinaryHelper.LoadFromBinary(config.BINARYPATH + typeof(T).Name + config.BINARYSUFFIX);
            LoadInternal(br);
            CompleteLoad();
        }

        private void LoadInternal(BinaryReader br)
        {
            var tempDict = new Dictionary<UniqueKey, T>();
            var count = br.ReadInt32();

            for (int i = 0; i < count; ++i)
            {
                long rawKey = br.ReadInt64();
                UniqueKey key = new UniqueKey(rawKey);
                T newData = ReadFromBinary(br, key);
                
                tempDict.TryAdd(newData.Key, newData);
            }

            DataList = tempDict.ToImmutableDictionary();
        }
    }
}
