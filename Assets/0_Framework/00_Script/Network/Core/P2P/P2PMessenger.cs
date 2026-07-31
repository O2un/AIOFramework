using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using R3;

namespace O2un.Core.Network
{
    public sealed class P2PMessenger : NetworkMessengerBase<NetworkPacketId, ReadOnlyMemory<byte>>, IP2PMessenger
    {
        private readonly IP2PTransport _transport;

        public P2PMessenger(IP2PTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.PacketReceived.Subscribe(ProcessIncomingPacket).AddTo(DisposableR3);
        }

        public ReadOnlyReactiveProperty<bool> IsConnected => _transport.IsConnected;

        protected override bool RoutesMismatchedResponse => true;

        public Observable<T> Observe<T>(NetworkPacketId eventId)
        {
            ThrowIfDisposed();
            ThrowIfInvalidEventId(eventId);
            return ObserveEvent<T>(eventId);
        }

        public async UniTask<bool> SendDataAsync<T>(NetworkPacketId eventId, NetworkPacketType packetType, T data, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ValidatePacket(eventId, packetType);

            using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(ct, LifetimeToken);
            var packet = CreateRequestPacket(eventId, packetType, 0, data);
            return await _transport.SendAsync(packet, operationSource.Token);
        }

        public UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(NetworkPacketId eventId, NetworkPacketType packetType, TRequest data, TimeSpan timeout, CancellationToken ct = default)
        {
            return SendDataAndWaitAsync<TRequest, TResponse>(eventId, eventId, packetType, data, timeout, ct);
        }

        public UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(NetworkPacketId eventId, NetworkPacketId responseEventId, NetworkPacketType packetType, TRequest data, TimeSpan timeout, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ValidatePacket(eventId, packetType);

            return RequestAsync<TResponse>(
                responseEventId,
                timeout,
                (sequence, token) => _transport.SendAsync(CreateRequestPacket(eventId, packetType, sequence, data), token),
                ct);
        }

        protected override bool IsEventIdValid(NetworkPacketId eventId)
        {
            return eventId.IsDefined();
        }

        protected override T Deserialize<T>(ReadOnlyMemory<byte> payload)
        {
            return NetworkJson.Deserialize<T>(payload.Span);
        }

        private static P2PRequestPacket CreateRequestPacket<T>(NetworkPacketId eventId, NetworkPacketType packetType, ulong sequence, T data)
        {
            byte[] payload = NetworkJson.SerializeToUtf8Bytes(data);
            return new P2PRequestPacket(eventId, packetType, sequence, payload);
        }

        private void ProcessIncomingPacket(P2PInboundPacket packet)
        {
            // Packet Type은 P2P wire 전용이라 공통 기반이 모른다. 알 수 없는 값은 넘기기 전에 버린다.
            if (false == packet.PacketType.IsDefined())
            {
                Log.Print(Log.LogLevel.Warning, $"정의되지 않은 P2P Packet Type을 버렸다. packetType={(int)packet.PacketType}", Log.LogFilter.Server);
                return;
            }

            try
            {
                NetworkDispatchResult result = Dispatch(packet.Sequence, packet.EventId, packet.Payload);
                if (NetworkDispatchResult.InvalidEvent == result)
                {
                    Log.Print(Log.LogLevel.Warning, $"정의되지 않은 P2P Event ID를 버렸다. eventId={(int)packet.EventId}", Log.LogFilter.Server);
                    return;
                }

                if (NetworkDispatchResult.ResponseEventMismatch == result)
                {
                    Log.Print(Log.LogLevel.Warning, $"P2P 응답 Event ID가 일치하지 않는다. sequence={packet.Sequence}, eventId={packet.EventId}", Log.LogFilter.Server);
                }
            }
            catch (Exception ex)
            {
                // 전송 콜백에서 예외가 나가면 구독 자체가 끊긴다.
                Log.Print(Log.LogLevel.Error, $"P2P 패킷 처리 실패. eventId={(int)packet.EventId}, error={ex.Message}", Log.LogFilter.Server);
            }
        }

        private void ValidatePacket(NetworkPacketId eventId, NetworkPacketType packetType)
        {
            ThrowIfInvalidEventId(eventId);
            if (false == packetType.IsDefined())
            {
                throw new ArgumentOutOfRangeException(nameof(packetType));
            }
        }
    }
}
