using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.SceneManagement;
using O2un.Utils;

namespace O2un.Core
{
    public sealed class SceneManager : EngineSubsystemBase
    {
        public enum SceneState
        {
            Idle,
            TransitioningToLoading,
            LoadingTarget,
            TransitioningToTarget
        }

        private const string LOADING_SCENE_NAME = "LoadingScene";

        private readonly ReactiveProperty<SceneState> _currentState = new(SceneState.Idle);
        private readonly ReactiveProperty<float> _loadingProgress = new(0f);
        public ReadOnlyReactiveProperty<SceneState> CurrentState => _currentState;
        public ReadOnlyReactiveProperty<float> LoadingProgress => _loadingProgress;

        protected override void SafeDispose()
        {
            _currentState.Dispose();
            _loadingProgress.Dispose();
        }

        protected override async UniTask InitAsync()
        {
            await UniTask.CompletedTask;
        }

        public async UniTask LoadSceneAsync(string targetSceneName)
        {
            if (_currentState.Value != SceneState.Idle)
            {
                return;
            }
            
            await this.StartExclusiveAsync("SceneLoad", async ct =>
            {
                try
                {
                    _currentState.Value = SceneState.TransitioningToLoading;
                    _loadingProgress.Value = 0f;
                    
                    await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(LOADING_SCENE_NAME, LoadSceneMode.Single).WithCancellation(ct);

                    _currentState.Value = SceneState.LoadingTarget;
                    
                    var loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
                    loadOp.allowSceneActivation = false;

                    while (loadOp.progress < 0.9f)
                    {
                        ct.ThrowIfCancellationRequested();
                        _loadingProgress.Value = loadOp.progress;
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    _loadingProgress.Value = 1f;
                    _currentState.Value = SceneState.TransitioningToTarget;

                    loadOp.allowSceneActivation = true;
                    
                    await loadOp.WithCancellation(ct);
                }
                finally
                {
                    _currentState.Value = SceneState.Idle;
                    if (ct.IsCancellationRequested)
                    {
                        _loadingProgress.Value = 0f;
                    }
                }
            });
        }
    }
}
