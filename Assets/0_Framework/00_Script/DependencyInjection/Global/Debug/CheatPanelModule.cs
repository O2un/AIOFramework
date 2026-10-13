using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.DI
{
    public sealed class CheatPanelModule : IDebugModule
    {
        public const string KEY = "CheatPanel";
        private const string ADDRESS = "UI/Debug/CheatPanel";

        public string Key => KEY;
        public string Address => ADDRESS;

        public UniTask WaitUntilReadyAsync(CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }
    }
}
