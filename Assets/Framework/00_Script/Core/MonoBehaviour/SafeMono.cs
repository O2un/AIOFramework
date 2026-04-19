using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace O2un
{
    public abstract class SafeMono : MonoBehaviour
    {
        private bool _isinit = false;
        private bool _isInitializing = false;
        private UniTaskCompletionSource _initCompletionSource;

#region OBSOLETE_UNITY_EVENT
        [Obsolete("SafeMono에서는 Start()를 사용할 수 없습니다. Init() 또는 LinkDependency()를 오버라이드 하세요.", true)]
        private void Start() {}
#endregion

        private void Awake()
        {
            if(_isinit || _isInitializing)
            {
                return;
            }

            _isInitializing = true;
            _initCompletionSource = new();

            _=InitPipelineAsync();
        }
        private async UniTaskVoid InitPipelineAsync()
        {
            try
            {
                await Init();
                _isinit = true;
                _initCompletionSource.TrySetResult();
                await LinkDependency();
                
                if(isActiveAndEnabled)
                {   
                    RegisterUpdate();
                }
            }
            catch (Exception e)
            {
                _initCompletionSource.TrySetException(e);
            }
        }
        public UniTask EnsureInitializedAsync()
        {
            if (_isinit) return UniTask.CompletedTask;
            return _initCompletionSource.Task;
        }

        private void RegisterUpdate()
        {
            if (this is ISafeUpdate updateable)
            {
                var system = SystemProvider.GetSubsystem<GameUpdateSubSystem>();
                system?.Register(updateable);
            }

            if (this is ISafeFixedUpdate fu)
            {
                var system = SystemProvider.GetSubsystem<GameFixedSubsystem>();
                system?.Register(fu);
            }

            if (this is ISafeLateUpdate lu)
            {
                var system = SystemProvider.GetSubsystem<GameLateSubsystem>();
                system?.Register(lu);
            }
        }
        private void UnregisterUpdate()
        {
            if (!_isinit) return;

            if (this is ISafeUpdate updateable)
            {                
                SystemProvider.GetSubsystem<GameUpdateSubSystem>()?.Unregister(updateable);
            }

            if (this is ISafeFixedUpdate fu)
            {                
                SystemProvider.GetSubsystem<GameFixedSubsystem>()?.Unregister(fu);
            }

            if (this is ISafeLateUpdate lu)
            {                
                SystemProvider.GetSubsystem<GameLateSubsystem>()?.Unregister(lu);
            }
        }

        private void OnEnable()
        {
            if(_isinit)
            {
                RegisterUpdate();
            }
            SafeEnable();
        }
        protected virtual void SafeEnable()
        {
            
        }
        private void OnDisable()
        {
            UnregisterUpdate();
            _disposableR3.Clear();
            SafeDisable();
        }

        protected virtual void SafeDisable()
        {
            
        }
        private void OnDestroy()
        {
            if (_isinit && gameObject.activeInHierarchy) 
            {
                UnregisterUpdate();
            }

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
        /// <summary>
        /// Handle object linking and dependency setup within this function.
        /// </summary>
        /// <returns></returns>
        protected async virtual UniTask LinkDependency()
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
