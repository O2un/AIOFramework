using Cysharp.Threading.Tasks;
using Unity.Entities;
using UnityEngine;

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
    }
}
