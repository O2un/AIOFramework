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

            await _sceneManager.LoadSceneAsync("LobbyScene");
        }
    }
}
