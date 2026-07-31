using System;
using System.IO;
using O2un.Core.Utils;
using UnityEngine;

namespace O2un.Core.Data
{
    /// <summary>
    /// <see cref="Application.persistentDataPath"/> 아래 JSON 파일 하나와 1:1로 대응하는 런타임 설정.
    /// <see cref="GlobalConfig{T}"/>가 빌드 후 읽기 전용인 것과 달리 실행 중에 쓰고 다시 읽을 수 있다.
    /// </summary>
    public interface IRuntimeData
    {
        string Key { get; }
        void Load();
        void Save();
    }

    /// <summary>
    /// <see cref="IRuntimeData"/>의 JSON 입출력 구현. 파생 타입은 자기 자신을 <typeparamref name="T"/>로 넘긴다.
    /// </summary>
    public abstract class RuntimeData<T> : IRuntimeData where T : RuntimeData<T>, new()
    {
        public abstract string Key { get; }

        private string FilePath => Path.Combine(Application.persistentDataPath, $"{Key}.json");

        public void Load()
        {
            string path = FilePath;

            if(false == File.Exists(path))
            {
                Save();
                return;
            }

            try
            {
                // FromJsonOverwrite 는 JSON 에 없는 키를 건드리지 않는다. 파일이 일부만 채워져 있어도 나머지 필드는 기본값이 살아남는다.
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), this);
            }
            catch(Exception e)
            {
                Log.Print(Log.LogLevel.Error, $"런타임 데이터 로드 실패 ({path}): {e.Message}");
            }
        }

        public void Save()
        {
            string path = FilePath;

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(this, true));
            }
            catch(Exception e)
            {
                Log.Print(Log.LogLevel.Error, $"런타임 데이터 저장 실패 ({path}): {e.Message}");
            }
        }
    }
}
