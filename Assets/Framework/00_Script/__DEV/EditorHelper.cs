using System.IO;
using UnityEngine;

#if UNITY_EDITOR 
    using UnityEditor;
    public static class EditorHelper
    {
        public static T GetOrCreateSettings<T>(string path) where T : SystemConfig<T>
        {
            T settings = AssetDatabase.LoadAssetAtPath<T>(path);
    
            if (settings == null)
            {
                // 폴더가 없는 경우를 대비해 디렉토리 생성
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
    
                settings = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Settings] 새 설정 파일이 생성되었습니다: {path}");
            }
    
            return settings;
        }
    }
#endif
