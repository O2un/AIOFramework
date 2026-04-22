using System;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using O2un.DI;

namespace O2un
{
    public abstract partial class SubSystemBase : IAsyncReady, IDisposable
    {
        private readonly UniTaskCompletionSource _readySource = new();
        protected SubSystemBase()
        {
            _ = InternalInitAsync();
        }
        private async UniTaskVoid InternalInitAsync()
        {
            try
            {
                await InitAsync();
                _readySource.TrySetResult();
            }
            catch(Exception e)
            {
                _readySource.TrySetException(e);
                Log.Print(Log.LogLevel.Fatal, $"[{GetType().Name}] Init Failed: {e.Message}");
            }
        }
        protected abstract UniTask InitAsync();
        public virtual void Dispose()
        {
        }
        public UniTask WaitUntilReadyAsync() => _readySource.Task;
    }
    
    public abstract partial class EngineSubsystemBase : SubSystemBase { }
    public abstract partial class GameSubsystemBase : SubSystemBase { }
    #if UNITY_EDITOR
    public abstract partial class EditSubsystemBase : SubSystemBase { }
    #endif
}
