using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.DI;
using R3;
using UnityEngine;

namespace O2un
{
    public abstract class SafeMono : MonoBehaviour, IAsyncReady
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
                await Init();
                _state = ReadyState.Ready;
                ReadyCompletionSource.TrySetResult();
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
            _state = ReadyState.Disposed;
            _disposableR3.Dispose();
            SafeDestroy();
        }
        protected virtual void SafeDestroy()
        {
            
        }
        
        /// <summary>
        /// Limit this function to self-initialization and resource loading only.
        /// </summary>
        /// <returns></returns>
        protected async virtual UniTask Init()
        {
            await UniTask.CompletedTask;
        }

        #region ASYNC_HELPER
        protected readonly CompositeDisposable _disposableR3 = new();
        protected void DelayCall(float second, Action action, bool ignoreTimeScale = false , UnityTimeProvider timing = null)
        {
            var timeProvider = timing ?? UnityTimeProvider.Update;
            var selectedProvider = ignoreTimeScale ? TimeProvider.System : timeProvider;
            Observable.Timer(TimeSpan.FromSeconds(second), selectedProvider)
                .Subscribe(_ => 
                {
                    action();
                })
                .AddTo(_disposableR3);
        }
        
        public delegate UniTask Task(CancellationToken ct);
        protected IDisposable StartAsync(Func<CancellationToken, UniTask> taskFunc)
        {
            var disposable = Observable.FromAsync(async ct =>
            {
                try
                {
                    await taskFunc(ct);
                }
                catch (OperationCanceledException) {}
            }).Subscribe();
            disposable.AddTo(_disposableR3);
            return disposable;
        }

        private readonly Dictionary<string, SerialDisposable> _exclusiveTasks = new();
        protected void StartExclusiveAsync(string key, Func<CancellationToken, UniTask> taskFunc)
        {
            //RuntimeAuditor.AssertStringIsLiteral(key);

            if (!_exclusiveTasks.TryGetValue(key, out var serialHandler))
            {
                serialHandler = new SerialDisposable();
                serialHandler.AddTo(_disposableR3);
                _exclusiveTasks.Add(key, serialHandler);
            }
            
            serialHandler.Disposable = Observable.FromAsync(async ct =>
            {
                try
                {
                    await taskFunc(ct);
                }
                catch (OperationCanceledException){}
            }).Subscribe();
        }

        protected void CancelExclusiveAsync(string key)
        {
            if (_exclusiveTasks.TryGetValue(key, out var serialHandler))
            {
                serialHandler.Disposable = null; 
            }
        }
        #endregion
    }
}
