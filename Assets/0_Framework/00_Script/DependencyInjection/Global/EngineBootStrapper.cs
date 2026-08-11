using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    public class EngineBootStrapper : IAsyncStartable
    {
        private readonly IEnumerable<IRootTask> _rootTasks;
        private readonly IEnumerable<IStartupTask> _startupTasks;
        private readonly ISceneManager _sceneManager;

        [Inject]
        public EngineBootStrapper(IEnumerable<IRootTask> rootTasks, IEnumerable<IStartupTask> startupTasks, ISceneManager sceneManager)
        {
            _rootTasks = rootTasks;
            _startupTasks = startupTasks;
            _sceneManager = sceneManager;
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            await UniTask.WhenAll(_rootTasks.Select(task => task.WaitUntilReadyAsync()));

            await UniTask.WhenAll(_startupTasks.Select(task => task.StartupTaskAsync()));

            BootConfig config = BootConfig.LoadRuntime();

            string startScene = null != config ? config.ResolveStartScene() : BootConfig.DEFAULT_START_SCENE;

            // 이미 그 씬에서 시작했으면 다시 열지 않는다. 다시 열면 초기화 중이던 View 가 통째로 파괴되고,
            // 그 View 들은 await 뒤에 Model 을 다시 확인하지 않아 NullReference 를 던진다.
            if (startScene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
            {
                return;
            }

            await _sceneManager.LoadSceneAsync(startScene);
        }
    }
}
