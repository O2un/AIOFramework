using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using O2un.Utils;

namespace O2un.Core
{
    public interface ISceneManager : IAsyncReady
    {
        UniTask LoadSceneAsync(string targetSceneName);
    }

    public sealed class SceneManager : EngineSubsystemBase, ISceneManager
    {
        public enum SceneState
        {
            Idle,
            TransitioningToLoading,
            LoadingTarget,
            TransitioningToTarget
        }

        private const string LOADING_SCENE_NAME = "LoadingScene";

        /// <summary>초당 채워지는 로딩바 비율. 1f면 0 → 1까지 최소 1초.</summary>
        private const float PROGRESS_SPEED = 1.0f;

        private readonly ReactiveProperty<SceneState> _currentState = new(SceneState.Idle);
        public ReadOnlyReactiveProperty<SceneState> CurrentState => _currentState;

        // 진행률은 Provider가 소유한다. SceneManager는 쓰기만 하고, UI는 같은 Runtime을 읽는다.
        private readonly LoadingRuntime _loadingRuntime;

        public SceneManager(ILoadingProvider provider)
        {
            _loadingRuntime = provider.GetRuntime(LoadingType.Scene);
        }

        protected override void SafeDispose()
        {
            _currentState.Dispose();
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
                    _loadingRuntime.Set(0f);
                    
                    await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(LOADING_SCENE_NAME, LoadSceneMode.Single).WithCancellation(ct);

                    _currentState.Value = SceneState.LoadingTarget;
                    
                    var loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
                    loadOp.allowSceneActivation = false;

                    float displayProgress = 0f;
                    while (loadOp.progress < 0.9f)
                    {
                        ct.ThrowIfCancellationRequested();
                        displayProgress = Mathf.MoveTowards(displayProgress, loadOp.progress, Time.deltaTime * PROGRESS_SPEED);
                        _loadingRuntime.Set(displayProgress);
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    while (displayProgress < 1f)
                    {
                        ct.ThrowIfCancellationRequested();
                        displayProgress = Mathf.MoveTowards(displayProgress, 1f, Time.deltaTime * PROGRESS_SPEED);
                        _loadingRuntime.Set(displayProgress);
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    _currentState.Value = SceneState.TransitioningToTarget;

                    loadOp.allowSceneActivation = true;
                    
                    await loadOp.WithCancellation(ct);
                }
                finally
                {
                    _currentState.Value = SceneState.Idle;
                    if (ct.IsCancellationRequested)
                    {
                        _loadingRuntime.Set(0f);
                    }
                }
            });
        }
    }
}
