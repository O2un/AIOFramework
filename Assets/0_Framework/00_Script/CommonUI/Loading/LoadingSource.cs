using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Utils;
using R3;
using UnityEngine;

namespace O2un.UI
{
    /// <summary>
    /// Provider에 등록되지 않는 개발용 소스. 5초에 걸쳐 0 → 1을 스스로 채운다.
    /// </summary>
    public sealed class MockLoadingSource : SafeDisposableClass, ILoadingSource
    {
        private readonly ReactiveProperty<float> _progress = new();
        public ReadOnlyReactiveProperty<float> Progress => _progress;

        public MockLoadingSource()
        {
            this.StartAsync(async ct =>
            {
                await RunMockAsync(ct);
            });
        }

        private async UniTask RunMockAsync(CancellationToken token)
        {
            const float duration = 5f;
            float elapsed = 0f;

            try
            {
                _progress.Value = 0f;

                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    if (IsDisposed)
                    {
                        return;
                    }

                    elapsed += Time.deltaTime;
                    _progress.Value = Mathf.Clamp01(elapsed / duration);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (IsDisposed)
                {
                    return;
                }

                _progress.Value = 1f;
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override void SafeDispose()
        {
            _progress.Dispose();
        }
    }
}
