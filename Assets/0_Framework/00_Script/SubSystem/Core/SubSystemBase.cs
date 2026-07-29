using System;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Utils;
using VContainer.Unity;

namespace O2un
{
    public abstract partial class SubSystemBase : SafeDisposableClass, IAsyncReady, IInitializable
    {
        private readonly UniTaskCompletionSource _readySource = new();
        public void Initialize() => _ = InternalInitAsync();

        private async UniTaskVoid InternalInitAsync()
        {
            try
            {
                await InitAsync();
                _readySource.TrySetResult();
            }
            catch (Exception e)
            {
                _readySource.TrySetException(e);
                Log.Print(Log.LogLevel.Fatal, $"[{GetType().Name}] Init Failed: {e.Message}");
            }
        }
        protected abstract UniTask InitAsync();
        public UniTask WaitUntilReadyAsync() => _readySource.Task;
    }
    
    public abstract partial class EngineSubsystemBase : SubSystemBase, IRootTask { }
    public abstract partial class GameSubsystemBase : SubSystemBase { }
    #if UNITY_EDITOR
    public abstract partial class EditSubsystemBase : SubSystemBase { }
    #endif
}
