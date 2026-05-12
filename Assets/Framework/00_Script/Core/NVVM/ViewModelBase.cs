using System;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.DI;
using O2un.Roslyn.Analyzer;
using R3;

namespace O2un.MVVM
{
    public abstract class ViewModelBase : SafeDisposableClass, IAsyncReady
    {
        private readonly UniTaskCompletionSource _readySource = new();
        private bool _isInit;

        public void TryInit(bool isVisibleOnInit)
        {
            if(_isInit)
            {
                return;
            }
            _isInit = true;

            InternalInitAsync().ContinueWith(() =>
            {
                _isVisible.Value = isVisibleOnInit;
            });
        }
        private async UniTask InternalInitAsync()
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
        private readonly ReactiveProperty<bool> _isVisible = new(false);
        public ReadOnlyReactiveProperty<bool> IsVisible => _isVisible;

        protected void SetVisible(bool visible)
        {
            _isVisible.Value = visible;
        }
    }
}
