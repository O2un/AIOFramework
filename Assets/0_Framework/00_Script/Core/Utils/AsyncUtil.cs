using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using R3;

namespace O2un.Utils
{
    public readonly struct AsyncHandle : IDisposable
    {
        private readonly UniTask _task;
        private readonly IDisposable _disposable;
        public AsyncHandle(UniTask task, IDisposable disposable)
        {
            _task = task;
            _disposable = disposable;
        }
        public void Dispose() => _disposable?.Dispose();
        public UniTask.Awaiter GetAwaiter() => _task.GetAwaiter();
    }
    
    public static class AsyncUtils
    {
        public delegate UniTask TaskFunction(CancellationToken ct);

        public static (CancellationToken token, IDisposable trigger) CreateCancelContext()
        {
            var cts = new CancellationTokenSource();
            var trigger = Disposable.Create(() => 
            {
                cts.Cancel();
                cts.Dispose();
            });
            return (cts.Token, trigger);
        }

        public static AsyncHandle DelayCall(this ISafeDisposable safeClass, float seconds, Action action, bool ignoreTimeScale = false)
        {
            return safeClass.StartAsync(async ct =>
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: ignoreTimeScale, cancellationToken: ct);
                action?.Invoke();
            });
        }

        public static AsyncHandle DelayAsync(this ISafeDisposable safeClass, float seconds, bool ignoreTimeScale = false)
        {
            return safeClass.StartAsync(async ct =>
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: ignoreTimeScale, cancellationToken: ct);
            });
        }

        private static async UniTask WrapTaskAsync(ISafeDisposable safeClass, TaskFunction taskFunc, CancellationToken ct, IDisposable cancelTrigger)
        {
            try
            {
                await taskFunc(ct);
            }
            finally
            {
                safeClass.DisposableR3.Remove(cancelTrigger);
                cancelTrigger.Dispose();
            }
        }

        public static AsyncHandle StartAsync(this ISafeDisposable safeClass, TaskFunction taskFunc)
        {
            var (token, trigger) = CreateCancelContext();
            safeClass.DisposableR3.Add(trigger);
            var task = WrapTaskAsync(safeClass, taskFunc, token, trigger);
            return new(task, trigger);
        }

        public static AsyncHandle StartExclusiveAsync(this ISafeDisposable safeClass, string key, TaskFunction taskFunc)
        {
            // RuntimeAuditor.AssertStringIsLiteral(key);

            if (!safeClass.ExclusiveTasks.TryGetValue(key, out var serialHandler))
            {
                serialHandler = new SerialDisposable();
                serialHandler.AddTo(safeClass.DisposableR3);
                safeClass.ExclusiveTasks.Add(key, serialHandler);
            }
            
            var (token, trigger) = CreateCancelContext();
            serialHandler.Disposable = trigger;
            
            var task = WrapExclusiveTaskAsync(taskFunc, token);
            return new AsyncHandle(task, trigger);
        }

        private static async UniTask WrapExclusiveTaskAsync(TaskFunction taskFunc, CancellationToken ct)
        {
            try
            {
                await taskFunc(ct);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}