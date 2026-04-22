using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Utils;
using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    public class EngineBootStrapper : IAsyncStartable
    {
        private readonly IEnumerable<IAsyncReady> _engineSubsystems;
        private readonly SceneManager _sceneManager;

        [Inject]
        public EngineBootStrapper(IEnumerable<IAsyncReady> engineSubsystems,SceneManager sceneManager)
        {
            _engineSubsystems = engineSubsystems;
            _sceneManager = sceneManager;
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            Log.Print(Log.LogLevel.Info, "[EngineBootstrap] 엔진 코어 시스템들 초기화 대기 시작...");
            var waitTasks = _engineSubsystems.Select(system => system.WaitUntilReadyAsync());
            await UniTask.WhenAll(waitTasks);
            Log.Print(Log.LogLevel.Info, "[EngineBootstrap] 모든 코어 시스템 준비 완료! Valid 상태 진입.");

            _=_sceneManager.LoadSceneAsync("LobbyScene");
        }
    }
}
