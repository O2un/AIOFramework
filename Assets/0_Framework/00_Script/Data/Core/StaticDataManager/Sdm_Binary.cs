using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using O2un.Core;
using O2un.Data.Binary;

namespace O2un.Data 
{
    public abstract partial class StaticDataManager<T> : IStaticDataManager where T : StaticData, new()
    {
        /// <summary>
        /// 구운 바이너리가 놓일 폴더. 게임이 소유한 Excel 에서 나온 매니저는 이걸 덮어써
        /// 게임 폴더로 간다 — override 가 빠지면 게임 데이터가 프레임워크 폴더에 구워지고
        /// upstream 싱크마다 충돌한다. 그래서 ExcelDataPostprocessor 가 생성 파일에 직접 써 넣는다.
        /// </summary>
        protected virtual string ResolveBinaryDirectory(StaticDataConfig config)
        {
            return config.BINARYPATH;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void SaveToBinary()
        {
            var config = StaticDataConfig.GetConfig();
            using var bw = BinaryHelper.SaveToBinary(ResolveBinaryDirectory(config) + typeof(T).Name + config.BINARYSUFFIX);
            bw.Write(DataList.Count);
            foreach (var d in DataList)
            {
                bw.Write(d.Key.Raw);
                WriteToBinary(bw, d.Value);
            }
        }

        protected abstract void WriteToBinary(BinaryWriter bw, T data);
        protected abstract T ReadFromBinary(BinaryReader br, UniqueKey key);

        protected void LoadFromBinary()
        {
            var config = StaticDataConfig.LoadRuntime();

            using var br = BinaryHelper.LoadFromBinary(ResolveBinaryDirectory(config) + typeof(T).Name + config.BINARYSUFFIX);
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
