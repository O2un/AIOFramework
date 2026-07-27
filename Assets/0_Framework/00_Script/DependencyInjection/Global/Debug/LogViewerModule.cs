using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using VContainer;

namespace O2un.DI
{
    /// <summary>
    /// 로그 뷰어(LogContext) 모듈.
    /// LogVM 이 생성 직후 로그 파일을 읽으므로 LogManager 가 준비된 뒤에 인스턴스화한다.
    /// </summary>
    public sealed class LogViewerModule : IDebugModule
    {
        public const string KEY = "LogViewer";
        private const string ADDRESS = "UI/Debug/LogViewer";

        private readonly ILogManager _logManager;

        [Inject]
        public LogViewerModule(ILogManager logManager)
        {
            _logManager = logManager;
        }

        public string Key => KEY;
        public string Address => ADDRESS;

        public UniTask WaitUntilReadyAsync(CancellationToken ct)
        {
            return _logManager.WaitUntilReadyAsync().AttachExternalCancellation(ct);
        }
    }
}
