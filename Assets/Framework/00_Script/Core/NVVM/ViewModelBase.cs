using System;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.DI;
using O2un.Roslyn.Analyzer;
using R3;

namespace O2un.NVVM
{
    public abstract class ViewModelBase : SafeDisposableClass, IAsyncReady
    {
        private readonly UniTaskCompletionSource _readySource = new();

        public void TryInit()
        {
            _ = InternalInitAsync();
        }
        private async UniTaskVoid InternalInitAsync()
        {
            try
            {
                await InitAsync();
                _readySource.TrySetResult(); // 준비 완료 신호
            }
            catch (Exception e)
            {
                _readySource.TrySetException(e);
                UnityEngine.Debug.LogError($"[{GetType().Name}] ViewModel 초기화 실패: {e.Message}");
            }
        }

        public virtual UniTask InitAsync() => UniTask.CompletedTask;
        public UniTask WaitUntilReadyAsync() => _readySource.Task;
    }
}
