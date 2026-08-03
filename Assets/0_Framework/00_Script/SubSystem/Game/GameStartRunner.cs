using System;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Network;
using O2un.Core.Utils;
using R3;
using VContainer.Unity;

namespace O2un
{
    /// <summary>
    /// 게임 시작 전환을 한쪽에서 소유한다. Host 는 Broadcast 를 보내고 자기 Scene 을 넘기며,
    /// Broadcast 를 받은 Client 는 같은 경로로 Scene 을 넘긴다.
    /// </summary>
    public sealed class GameStartRunner : IInitializable, IDisposable
    {
        private const string GAME_SCENE_NAME = "GameScene";

        private readonly IMultiplayerManager _multiplayer;
        private readonly ISceneManager _sceneManager;
        private readonly GameStartP2PModule _gameStart;
        private readonly CompositeDisposable _disposables = new();

        // 세션마다 Messenger 가 새로 열린다. 이전 세션 스트림 구독을 놓지 않으면 세션 수만큼 겹쳐 받는다.
        private readonly SerialDisposable _receiveSubscription = new();

        public GameStartRunner(IMultiplayerManager multiplayer, ISceneManager sceneManager)
        {
            _multiplayer = multiplayer;
            _sceneManager = sceneManager;
            _gameStart = new GameStartP2PModule(multiplayer);
        }

        public void Initialize()
        {
            _receiveSubscription.AddTo(_disposables);

            // Observe 는 구독 시점의 Messenger 를 잡는다. 방에 들어가기 전에 구독하면 빈 스트림이 고정된다.
            _multiplayer.Session.State
                        .Where(state => NetcodeSessionState.InLobby == state)
                        .Subscribe(_ => SubscribeReceived())
                        .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        public void RequestStart()
        {
            StartAsync().Forget();
        }

        private void SubscribeReceived()
        {
            _receiveSubscription.Disposable = _gameStart.Received.Subscribe(HandleReceived);
        }

        private void HandleReceived(GameStartPayload payload)
        {
            Log.Dev($"[GameStart] 시작 통지 수신 hostId={payload.HostId}");

            _sceneManager.LoadSceneAsync(GAME_SCENE_NAME).Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            // Broadcast 는 Sender 를 제외하고 나가므로 Host 의 전환은 여기서 직접 걸어야 한다.
            if (false == await _gameStart.BroadcastAsync())
            {
                Log.Print(Log.LogLevel.Error, "[GameStart] 시작 통지를 보내지 못했다. Host 만 게임 Scene 으로 넘어간다.", Log.LogFilter.Server);
            }

            await _sceneManager.LoadSceneAsync(GAME_SCENE_NAME);
        }
    }
}
