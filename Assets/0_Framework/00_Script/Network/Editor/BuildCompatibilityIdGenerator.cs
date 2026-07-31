using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using O2un.Core.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace O2un.Core.Network.Editor
{
    /// <summary>
    /// 배포 묶음마다 한 번 호출해 호환성 ID 를 굽는다. Host 와 Client 실행 파일은 이 호출 뒤 굽는 여러 번의 빌드다.
    /// </summary>
    public static class BuildCompatibilityIdGenerator
    {
        public const string GENERATED_PATH = "Assets/0_Framework/00_Script/Network/Core/Netcode/Build/BuildCompatibilityIdSource.generated.cs";

        [MenuItem("O2un/Build/Generate Build Compatibility Id")]
        public static void Generate()
        {
            string id = Create();

            File.WriteAllText(GENERATED_PATH, BuildSource(id));
            AssetDatabase.ImportAsset(GENERATED_PATH, ImportAssetOptions.ForceUpdate);

            Log.Print(Log.LogLevel.Info, $"[BuildCompatibilityIdGenerator] 배포 묶음 호환성 ID 를 생성했다. id={id}", Log.LogFilter.Server);
        }

        public static string Create()
        {
            var suffix = new byte[BuildCompatibilityId.SUFFIX_LENGTH / 2];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(suffix);
            }

            string time = DateTime.UtcNow.ToString(BuildCompatibilityId.TIME_FORMAT, CultureInfo.InvariantCulture);
            string tail = BitConverter.ToString(suffix).Replace("-", string.Empty).ToLowerInvariant();

            return $"{time}-{tail}";
        }

        public static string BuildSource(string id)
        {
            return "namespace O2un.Core.Network\r\n"
                   + "{\r\n"
                   + "    // 빌드 파이프라인(BuildCompatibilityIdGenerator)이 배포 묶음마다 한 번 덮어쓴다. 손으로 고치지 않는다.\r\n"
                   + "    internal static class BuildCompatibilityIdSource\r\n"
                   + "    {\r\n"
                   + $"        internal const string VALUE = \"{id}\";\r\n"
                   + "    }\r\n"
                   + "}\r\n";
        }
    }

    public sealed class BuildCompatibilityIdValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (false == BuildCompatibilityId.IsValid(BuildCompatibilityId.Current))
            {
                throw new BuildFailedException(
                    $"[BuildCompatibilityIdValidator] 배포 묶음 호환성 ID 가 생성되지 않았다. 이 배포 묶음의 첫 빌드 전에 {nameof(BuildCompatibilityIdGenerator)}.{nameof(BuildCompatibilityIdGenerator.Generate)} 를 한 번 호출하고, 같은 묶음의 Host/Client 빌드는 그 값을 그대로 쓴다. value={BuildCompatibilityId.Current}");
            }
        }
    }
}
