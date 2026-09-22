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

        public string BinaryPath
        {
            get
            {
                var config = StaticDataConfig.LoadRuntime();
                return null == config ? string.Empty : ResolveBinaryPath(config);
            }
        }

        private string ResolveBinaryPath(StaticDataConfig config)
        {
            return ResolveBinaryDirectory(config) + typeof(T).Name + config.BINARYSUFFIX;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void SaveToBinary()
        {
            var config = StaticDataConfig.GetConfig();
            using var bw = BinaryHelper.SaveToBinary(ResolveBinaryPath(config));
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
            if (null == config)
            {
                return;
            }

            // 굽지 않은 데이터를 읽으면 여기서 null 이 온다. 그대로 넘기면 NRE 로 죽어
            // 어느 테이블이 비었는지가 사라진다. IsLoaded 를 false 로 남겨 진단이 집어낸다.
            using var br = BinaryHelper.LoadFromBinary(ResolveBinaryPath(config));
            if (null == br)
            {
                return;
            }

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
