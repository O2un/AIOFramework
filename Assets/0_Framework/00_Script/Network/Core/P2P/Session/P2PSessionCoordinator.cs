using System;
using O2un.Core.Utils;

namespace O2un.Core.Network
{
    /// <summary>
    /// 게임 세션 동안 Transport 와 Messenger 를 소유한다. 수명 경계는 Scene 이 아니라 매치 성립부터 연결 종료까지다.
    /// </summary>
    public sealed class P2PSessionCoordinator : SafeDisposableClass
    {
        private readonly Func<IP2PTransport> _transportFactory;

        private IP2PTransport _transport;
        private P2PMessenger _messenger;

        public P2PSessionCoordinator(Func<IP2PTransport> transportFactory)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        }

        public IP2PMessenger Messenger => _messenger;
        public bool HasSession => null != _messenger;

        protected override void SafeDispose()
        {
            CloseSession();

            base.SafeDispose();
        }

        /// <summary>
        /// WSS 입장 검증을 통과하고 Netcode 연결이 선 뒤에만 부른다. Client World 가 없으면 세션 없이 false 를 돌려준다.
        /// </summary>
        public bool OpenSession()
        {
            if (true == IsDisposed)
            {
                throw new ObjectDisposedException(nameof(P2PSessionCoordinator));
            }

            CloseSession();

            IP2PTransport transport = _transportFactory();

            if (null == transport)
            {
                return false;
            }

            _transport = transport;
            _messenger = new P2PMessenger(transport);

            return true;
        }

        public void CloseSession()
        {
            if (false == HasSession)
            {
                return;
            }

            P2PMessenger messenger = _messenger;
            IP2PTransport transport = _transport;
            _messenger = null;
            _transport = null;

            DisposeSafely(messenger, nameof(P2PMessenger));
            DisposeSafely(transport, nameof(IP2PTransport));
        }

        private static void DisposeSafely(IDisposable target, string name)
        {
            if (null == target)
            {
                return;
            }

            try
            {
                target.Dispose();
            }
            catch (Exception e)
            {
                Log.Print(Log.LogLevel.Error, $"[P2PSessionCoordinator] 세션 정리 중 오류가 발생했다. target={name}, error={e.Message}", Log.LogFilter.Server);
            }
        }
    }
}
