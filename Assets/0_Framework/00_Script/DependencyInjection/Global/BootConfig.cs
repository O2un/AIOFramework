using O2un.Core;
using UnityEngine;

namespace O2un.DI
{
    /// <summary>
    /// 엔진 부팅이 끝난 뒤 처음 여는 씬. <c>Resources/SystemConfig/BootConfig.asset</c> 을 읽는다.
    ///
    /// 루트 LifetimeScope 는 씬에 LifetimeScope 가 하나라도 있으면 만들어지고, 그 부팅이 항상 씬을 갈아끼운다.
    /// 이 값이 없으면 어떤 씬에서 Play 를 눌러도 1초 안에 그 씬이 사라져 씬 단위 작업이 불가능하다.
    /// </summary>
    public sealed class BootConfig : GlobalConfig<BootConfig>
    {
        public const string DEFAULT_START_SCENE = "LobbyScene";

        [Tooltip("부팅 후 진입할 씬 이름. 비우면 LobbyScene.")]
        public string StartScene = DEFAULT_START_SCENE;

        public string ResolveStartScene()
        {
            return true == string.IsNullOrWhiteSpace(StartScene) ? DEFAULT_START_SCENE : StartScene;
        }
    }
}
