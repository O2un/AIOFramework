using System;
using Cysharp.Threading.Tasks;
using O2un.Core.Network;
using O2un.Core.Utils;
using R3;
using VContainer.Unity;

namespace O2un.DI
{
    /// <summary>
    /// 로비 Scene 이 살아 있는 동안만 RelayCheck 를 세션에 붙인다.
    /// 어떤 기능 Module 을 쓰는지는 게임이 정하므로 프레임워크 Manager 는 이 기능을 알지 못한다.
    /// </summary>
    public sealed class RelayCheckLobbyRunner : IInitializable, IDisposable
    {
        private static readonly TimeSpan ECHO_TIMEOUT = TimeSpan.FromSeconds(5);

        private readonly IMultiplayerManager _multiplayer;
        private readonly RelayCheckP2PModule _relayCheck;
        private IDisposable _disposables;

        // 세션마다 Messenger 가 새로 열린다. 이전 세션 스트림 구독을 놓지 않으면 세션 수만큼 겹쳐 받는다.
        private readonly SerialDisposable _receiveSubscription = new();

        public RelayCheckLobbyRunner(IMultiplayerManager multiplayer)
        {
            _multiplayer = multiplayer;
            _relayCheck = new RelayCheckP2PModule(multiplayer);
        }

        public void Initialize()
        {
            var builder = Disposable.CreateBuilder();

            _receiveSubscription.AddTo(ref builder);

            _multiplayer.Session.State
                .Where(state => NetcodeSessionState.InLobby == state)
                .Subscribe(_ => EnterLobby())
                .AddTo(ref builder);

            _disposables = builder.Build();
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }

        private void EnterLobby()
        {
            _receiveSubscription.Disposable = _relayCheck.Received.Subscribe(HandleReceived);

            RunRelayCheckAsync().Forget();
        }

        private void HandleReceived(RelayCheckPayload payload)
        {
            Log.Dev($"[RelayCheck] 수신 senderId={payload.SenderId}, message={payload.Message}");
        }

        private async UniTaskVoid RunRelayCheckAsync()
        {
            int networkId = _multiplayer.Session.NetworkId.CurrentValue;

            bool isSent = await _relayCheck.BroadcastAsync($"broadcast from {networkId}");
            Log.Dev($"[RelayCheck] Broadcast 송신 isSent={isSent}");

            P2PPacketResult<RelayCheckPayload> echo = await _relayCheck.EchoAsync($"echo from {networkId}", ECHO_TIMEOUT);

            if (false == echo.IsSuccess)
            {
                Log.Print(Log.LogLevel.Error, $"[RelayCheck] Echo 응답을 받지 못했다. reason={echo.Reason}", Log.LogFilter.Server);
                return;
            }

            Log.Dev($"[RelayCheck] Echo 응답 senderId={echo.Value.SenderId}, message={echo.Value.Message}");
        }
    }
}
