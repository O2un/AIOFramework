using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Roslyn.Analyzer;
using R3;
using UnityEngine.SceneManagement;

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
        private CancellationTokenSource _loadCts;
        public ReadOnlyReactiveProperty<SceneState> CurrentState => _currentState;
        public ReadOnlyReactiveProperty<float> LoadingProgress => _loadingProgress;

        public override void Dispose()
        {
            CancelCurrentLoad();
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

            CancelCurrentLoad();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                _currentState.Value = SceneState.TransitioningToLoading;
                _loadingProgress.Value = 0f;

                await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(LOADING_SCENE_NAME, LoadSceneMode.Single).WithCancellation(token);

                _currentState.Value = SceneState.LoadingTarget;

                var loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
                loadOp.allowSceneActivation = false;

                while (loadOp.progress < 0.9f)
                {
                    token.ThrowIfCancellationRequested();
                    _loadingProgress.Value = loadOp.progress;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                _loadingProgress.Value = 1f;
                _currentState.Value = SceneState.TransitioningToTarget;

                loadOp.allowSceneActivation = true;

                await loadOp.WithCancellation(token);

                _currentState.Value = SceneState.Idle;
            }
            catch (OperationCanceledException)
            {
                _currentState.Value = SceneState.Idle;
                _loadingProgress.Value = 0f;
            }
            finally
            {
                if (_loadCts != null)
                {
                    _loadCts.Dispose();
                    _loadCts = null;
                }
            }
        }

        private void CancelCurrentLoad()
        {
            if (_loadCts != null)
            {
                _loadCts.Cancel();
                _loadCts.Dispose();
                _loadCts = null;
            }
        }
    }
}
