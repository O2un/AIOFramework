using Cysharp.Threading.Tasks;

namespace O2un.Core.Utils
{
    public sealed class LogManager : EngineSubsystemBase
    {
        protected override async UniTask InitAsync()
        {
            Log.Init();
            Log.Print(Log.LogLevel.Info, "LogManager Initialized");
            await UniTask.CompletedTask;
        }

        protected override void SafeDispose()
        {
            // NULL
        }
    }
}
