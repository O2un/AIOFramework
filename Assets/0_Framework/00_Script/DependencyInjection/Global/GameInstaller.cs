using System;
using UnityEngine;
using VContainer;

namespace O2un.DI
{
    /// <summary>
    /// 게임이 프레임워크 파일을 열지 않고 전역 서비스를 등록하는 자리.
    /// <c>GlobalConfig&lt;T&gt;</c> 를 상속한 ScriptableObject 에 이 인터페이스를 붙이고
    /// 자산을 <c>Resources/SystemConfig/</c> 아래 두면 부팅 때 자동으로 호출된다.
    /// </summary>
    public interface IGameInstaller
    {
        void Install(IContainerBuilder builder);
    }

    public static class GameInstaller
    {
        private const string RESOURCE_FOLDER = "SystemConfig";

        public static void RegisterGameInstallers(this IContainerBuilder builder)
        {
            ScriptableObject[] configs = Resources.LoadAll<ScriptableObject>(RESOURCE_FOLDER);

            // 정렬을 빼면 Installer 가 둘 이상일 때 같은 타입의 등록 승자가 실행마다 달라진다.
            Array.Sort(configs, (a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (ScriptableObject config in configs)
            {
                if (config is IGameInstaller installer)
                {
                    installer.Install(builder);
                }
            }
        }
    }
}
