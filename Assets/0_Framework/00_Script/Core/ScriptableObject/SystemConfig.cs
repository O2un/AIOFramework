using UnityEngine;
using System.IO;
using O2un.Core.Utils;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace O2un.Core
{
    public interface IEditorConfig { }
    public interface IGlobalConfig { }
    
    public abstract class SystemConfig<T> : ScriptableObject where T : SystemConfig<T>
    {
        protected static T GetOrCreateSettings(string path)
        {
    #if UNITY_EDITOR
            T settings = AssetDatabase.LoadAssetAtPath<T>(path);
            if (settings == null)
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
    
                settings = CreateInstance<T>();
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return settings;
    #else
            return null;
    #endif
        }
    }
    
    public abstract class EditorConfig<T> : SystemConfig<T>, IEditorConfig where T : EditorConfig<T>
    {
        public static string PATH => $"Assets/Framework/99_DEV/SystemConfig/{typeof(T).Name}.asset";
    
        public static T GetConfig() => GetOrCreateSettings(PATH);
    }
    
    public abstract class GlobalConfig<T> : SystemConfig<T>, IGlobalConfig where T : GlobalConfig<T>
    {
        public static string PATH => $"Assets/Resources/SystemConfig/{typeof(T).Name}.asset";
        public static string RUNTIME_PATH => $"SystemConfig/{typeof(T).Name}";
    
        public static T GetConfig() => GetOrCreateSettings(PATH);
    
        public static T LoadRuntime()
        {
            T settings = Resources.Load<T>(RUNTIME_PATH);
            if (settings == null)
            {
                Log.Print(Log.LogLevel.Error, $"런타임 설정 누락: {RUNTIME_PATH}");
            }
            return settings;
        }
    }
}