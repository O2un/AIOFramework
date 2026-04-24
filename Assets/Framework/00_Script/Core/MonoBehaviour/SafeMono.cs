using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.DI;
using R3;
using UnityEngine;

namespace O2un
{
    public abstract class SafeMono : MonoBehaviour, IAsyncReady, ISafeDisposable
    {
        public enum ReadyState
        {
            Created,
            Initializing,
            Ready,
            Failed,
            Disposed
        }

        private ReadyState _state = ReadyState.Created;
        public ReadyState State => _state;
        private UniTaskCompletionSource _readyCompletionSource;
        public UniTaskCompletionSource ReadyCompletionSource => _readyCompletionSource ??= new();

#region OBSOLETE_UNITY_EVENT
        [Obsolete("SafeMono에서는 Start()를 사용할 수 없습니다. Init() 또는 LinkDependency()를 오버라이드 하세요.", true)]
        private void Start() {}
#endregion

        private void Awake()
        {
            if(ReadyState.Created != _state) return;
            _state = ReadyState.Initializing;
            _=InitPipelineAsync();
        }
        private async UniTaskVoid InitPipelineAsync()
        {
            try
            {
                // 초기화 작업은 객체가 Disable되더라도 계속 돌아야 하기때문에 DisposableR3에 종속된 토큰이 아닌 객체 파괴 토큰을 사용한다
                var ct = this.GetCancellationTokenOnDestroy();
                await Init(ct);
                if (_state == ReadyState.Disposed) return;
                _state = ReadyState.Ready;
                ReadyCompletionSource.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                _state = ReadyState.Disposed;
                ReadyCompletionSource.TrySetCanceled();
            }
            catch (Exception e)
            {
                _state = ReadyState.Failed;
                ReadyCompletionSource.TrySetException(e);
                gameObject.SetActive(false); // or Destroy
            }
        }

        public UniTask WaitUntilReadyAsync()
        {
            if (_state == ReadyState.Ready) return UniTask.CompletedTask;
            if (_state == ReadyState.Failed) return UniTask.FromException(new Exception($"[{gameObject.name}] 객체가 Failed 상태입니다."));
            if (_state == ReadyState.Disposed) return UniTask.FromCanceled();

            return ReadyCompletionSource.Task;
        }
        
        private void OnEnable()
        {
            SafeEnable();
        }
        protected virtual void SafeEnable()
        {
            
        }
        private void OnDisable()
        {
            _disposableR3.Clear();
            SafeDisable();
        }

        protected virtual void SafeDisable()
        {
            
        }
        private void OnDestroy()
        {
            Dispose();
        }
        protected virtual void SafeDestroy()
        {
            
        }
        
        /// <summary>
        /// Limit this function to self-initialization and resource loading only.
        /// </summary>
        /// <returns></returns>
        protected async virtual UniTask Init(CancellationToken ct)
        {
            await UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _state = ReadyState.Disposed;
            _disposableR3.Dispose();
            _exclusiveTasks.Clear();
            
            SafeDestroy();
        }

        #region ASYNC_HELPER
        private bool _disposed = false;
        public bool IsDisposed => _disposed;
        protected readonly CompositeDisposable _disposableR3 = new();
        public CompositeDisposable DisposableR3 => _disposableR3;
        private readonly Dictionary<string, SerialDisposable> _exclusiveTasks = new();
        public Dictionary<string, SerialDisposable> ExclusiveTasks => _exclusiveTasks;
        #endregion
    }
}
