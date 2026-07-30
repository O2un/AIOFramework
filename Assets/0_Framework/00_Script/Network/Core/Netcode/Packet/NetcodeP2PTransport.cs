using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using R3;
using Unity.Collections;
using Unity.Entities;

namespace O2un.Core.Network
{
    /// <summary>
    /// 공통 논리 패킷과 Netcode RPC 사이의 유일한 변환 경계다. Entity·RPC·연결 타입은 이 경계 밖으로 나가지 않는다.
    /// </summary>
    public sealed class NetcodeP2PTransport : SafeDisposableClass, IP2PTransport
    {
        private readonly World _clientWorld;
        private readonly ReactiveProperty<bool> _isConnected = new(false);
        private readonly Subject<P2PInboundPacket> _packetReceived = new();
        private readonly Dictionary<P2PPeerId, int> _peerNetworkIds = new();
        private int _localNetworkId;

        internal NetcodeP2PTransport(World clientWorld)
        {
            if (null == clientWorld || false == clientWorld.IsCreated)
            {
                throw new ArgumentException("[NetcodeP2PTransport] 생성된 Client World 가 필요하다.", nameof(clientWorld));
            }

            _clientWorld = clientWorld;

            NetcodePacketBridge.PacketDrained += HandlePacketDrained;
            NetcodeConnectionBridge.NetworkIdChanged += HandleNetworkIdChanged;
        }

        public ReadOnlyReactiveProperty<bool> IsConnected => _isConnected;
        public Observable<P2PInboundPacket> PacketReceived => _packetReceived;

        internal int PeerCount => _peerNetworkIds.Count;

        public async UniTask<bool> SendAsync(P2PRequestPacket packet, CancellationToken ct = default)
        {
            if (true == IsDisposed)
            {
                throw new ObjectDisposedException(nameof(NetcodeP2PTransport));
            }

            ct.ThrowIfCancellationRequested();

            if (false == NetcodePacketWire.TryCreatePayload(packet.Payload.Span, out FixedList512Bytes<byte> payload))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(packet),
                    $"[NetcodeP2PTransport] payload 가 Netcode 상한을 넘었다. size={packet.Payload.Length}, max={NetcodePacketWire.MAX_PAYLOAD_BYTES}");
            }

            // EntityManager 는 메인 스레드만 다룰 수 있고 Send 는 임의 스레드에서 불릴 수 있다.
            await UniTask.SwitchToMainThread(ct);

            if (true == IsDisposed)
            {
                throw new ObjectDisposedException(nameof(NetcodeP2PTransport));
            }

            // 세션 종료는 Transport Dispose 보다 먼저 World 를 걷어낼 수 있다. 그때 EntityManager 를 만지면 죽는다.
            if (false == _clientWorld.IsCreated)
            {
                Log.Print(Log.LogLevel.Warning, $"[NetcodeP2PTransport] Client World 가 이미 정리돼 송신을 버렸다. eventId={(int)packet.EventId}", Log.LogFilter.Server);
                return false;
            }

            EntityManager entityManager = _clientWorld.EntityManager;
            var requestEntity = entityManager.CreateEntity();

            entityManager.AddComponentData(requestEntity, new ClientPacketSendRequest
            {
                EventId = (int)packet.EventId,
                PacketType = (int)packet.PacketType,
                SenderNetworkId = _localNetworkId,
                Sequence = packet.Sequence,
                Payload = payload,
            });

            return true;
        }

        protected override void SafeDispose()
        {
            NetcodePacketBridge.PacketDrained -= HandlePacketDrained;
            NetcodeConnectionBridge.NetworkIdChanged -= HandleNetworkIdChanged;

            _peerNetworkIds.Clear();
            _packetReceived.Dispose();
            _isConnected.Dispose();

            base.SafeDispose();
        }

        private void HandleNetworkIdChanged(int networkId)
        {
            if (true == IsDisposed)
            {
                return;
            }

            _localNetworkId = networkId;
            _isConnected.Value = 0 != networkId;

            if (0 == networkId)
            {
                _peerNetworkIds.Clear();
                return;
            }

            RegisterPeer(networkId);
        }

        private void HandlePacketDrained(NetcodeInboundPacketData packet)
        {
            if (true == IsDisposed)
            {
                return;
            }

            P2PPeerId senderId = RegisterPeer(packet.SenderNetworkId);

            _packetReceived.OnNext(new P2PInboundPacket(
                (NetworkPacketId)packet.EventId,
                (NetworkPacketType)packet.PacketType,
                senderId,
                packet.Sequence,
                packet.Payload));
        }

        private P2PPeerId RegisterPeer(int networkId)
        {
            if (0 == networkId)
            {
                return P2PPeerId.None;
            }

            var peerId = new P2PPeerId((ulong)networkId);
            _peerNetworkIds[peerId] = networkId;

            return peerId;
        }
    }
}
