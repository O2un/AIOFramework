using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using R3;
using UnityEngine;

namespace O2un.UI
{
    public interface ILoadingSource
    {
        public ReadOnlyReactiveProperty<float> Progress {get;}
    }

    public sealed class SceneLoadingSource : ILoadingSource
    {
        public ReadOnlyReactiveProperty<float> Progress => _scenemanager.LoadingProgress;
        private readonly SceneManager _scenemanager;
        public SceneLoadingSource(SceneManager sceneManager)
        {
            _scenemanager = sceneManager;
        }
    }

    public sealed class MockLoadingSource : ILoadingSource
    {
        private readonly ReactiveProperty<float> _progress01 = new();
        public ReadOnlyReactiveProperty<float> Progress => _progress01;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;
        public MockLoadingSource()
        {
            RunMockAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RunMockAsync(CancellationToken token)
        {
            const float duration = 5f;
            float elapsed = 0f;

            try
            {
                _progress01.Value = 0f;

                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    if (_disposed) return;

                    elapsed += Time.deltaTime;
                    _progress01.Value = Mathf.Clamp01(elapsed / duration);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (_disposed) return;

                _progress01.Value = 1f;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _progress01.Dispose();
        }
    }

    public enum LoadingType
    {
        Scene,
        Patch,
        Resources,
        Mock,
    }
}
