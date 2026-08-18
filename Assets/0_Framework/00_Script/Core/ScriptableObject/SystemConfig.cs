using System;
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

    /// <summary>
    /// 설정 자산이 만들어질 루트 폴더를 타입 쪽에서 정한다. 프레임워크 밖에서 정의한 설정이
    /// 프레임워크 폴더에 자산을 만들지 않게 하는 수단이다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ConfigAssetRootAttribute : Attribute
    {
        public string Root { get; }

        public ConfigAssetRootAttribute(string root)
        {
            Root = root;
        }
    }

    public abstract class SystemConfig<T> : ScriptableObject where T : SystemConfig<T>
    {
        private static string _assetRoot;

        protected static string ResolveAssetRoot(string defaultRoot)
        {
            if (null != _assetRoot)
            {
                return _assetRoot;
            }

            ConfigAssetRootAttribute attribute =
                (ConfigAssetRootAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(ConfigAssetRootAttribute));

            _assetRoot = null == attribute || true == string.IsNullOrWhiteSpace(attribute.Root)
                ? defaultRoot
                : attribute.Root.TrimEnd('/');

            return _assetRoot;
        }

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
        private const string DEFAULT_ROOT = "Assets/0_Framework/99_DEV";

        public static string PATH => $"{ResolveAssetRoot(DEFAULT_ROOT)}/SystemConfig/{typeof(T).Name}.asset";
    
        public static T GetConfig() => GetOrCreateSettings(PATH);
    }
    
    public abstract class GlobalConfig<T> : SystemConfig<T>, IGlobalConfig where T : GlobalConfig<T>
    {
        // ConfigAssetRoot 로 옮길 때도 끝은 Resources 폴더여야 한다. 아니면 RUNTIME_PATH 로 못 읽는다.
        private const string DEFAULT_ROOT = "Assets/Resources";

        public static string PATH => $"{ResolveAssetRoot(DEFAULT_ROOT)}/SystemConfig/{typeof(T).Name}.asset";
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