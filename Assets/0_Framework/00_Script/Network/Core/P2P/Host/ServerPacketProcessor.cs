using System;
using System.Collections.Generic;
using System.Text.Json;
using O2un.Core.Utils;

namespace O2un.Core.Network
{
    /// <summary>
    /// 전송 구현과 무관한 Host 검증·판정 계층이다. Client World 가 없는 Dedicated Server 도 같은 인스턴스를 쓴다.
    /// Sender 를 지어내 Process 를 직접 부르지 못하도록 어셈블리 밖에는 <see cref="IHostPacketHandlerRegistry"/> 만 노출한다.
    /// </summary>
    internal sealed class ServerPacketProcessor : SafeDisposableClass, IHostPacketHandlerRegistry
    {
        private readonly Dictionary<NetworkPacketId, IHostPacketHandler> _handlers = new();
        private readonly int[] _blockedCounts = new int[Enum.GetValues(typeof(PacketBlockReason)).Length];
        private readonly int _maxPayloadBytes;

        public ServerPacketProcessor(int maxPayloadBytes)
        {
            if (maxPayloadBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));
            }

            _maxPayloadBytes = maxPayloadBytes;
        }

        public int MaxPayloadBytes => _maxPayloadBytes;

        public int HandlerCount => _handlers.Count;

        public int BlockedCount(PacketBlockReason reason) => _blockedCounts[(int)reason];

        public void Register(IHostPacketHandler handler)
        {
            ThrowIfDisposed();

            if (null == handler)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (false == handler.EventId.IsDefined())
            {
                throw new ArgumentOutOfRangeException(nameof(handler));
            }

            if (true == _handlers.ContainsKey(handler.EventId))
            {
                throw new InvalidOperationException($"[ServerPacketProcessor] 이미 등록된 Event Handler 다. eventId={(int)handler.EventId}");
            }

            _handlers.Add(handler.EventId, handler);
        }

        public bool Unregister(NetworkPacketId eventId)
        {
            ThrowIfDisposed();
            return _handlers.Remove(eventId);
        }

        public PacketProcessResult Process(in PacketProcessContext context)
        {
            return Process(context, out _);
        }

        public PacketProcessResult Process(in PacketProcessContext context, out PacketBlockReason blockReason)
        {
            ThrowIfDisposed();

            if (false == context.EventId.IsDefined())
            {
                return Reject(context, PacketBlockReason.UndefinedEventId, out blockReason);
            }

            if (false == context.PacketType.IsDefined())
            {
                return Reject(context, PacketBlockReason.UnknownPacketType, out blockReason);
            }

            if (false == context.SenderId.IsValid)
            {
                return Reject(context, PacketBlockReason.InvalidSender, out blockReason);
            }

            if (_maxPayloadBytes < context.Payload.Length)
            {
                return Reject(context, PacketBlockReason.PayloadTooLarge, out blockReason);
            }

            if (false == _handlers.TryGetValue(context.EventId, out IHostPacketHandler handler))
            {
                return Reject(context, PacketBlockReason.HandlerNotRegistered, out blockReason);
            }

            if (false == handler.IsAllowedPacketType(context.PacketType))
            {
                return Reject(context, PacketBlockReason.PacketTypeNotAllowed, out blockReason);
            }

            PacketProcessResult result;

            try
            {
                result = handler.Handle(context);
            }
            catch (JsonException e)
            {
                Log.Dev($"[ServerPacketProcessor] payload 를 해석하지 못해 차단했다. eventId={(int)context.EventId}, senderId={context.SenderId}, error={e.Message}", Log.LogLevel.Warning);
                return Count(PacketBlockReason.DeserializeFailed, out blockReason);
            }
            catch (Exception e)
            {
                // JsonException 이 아닌 예외는 Client 입력이 아니라 서버 Handler 결함이다. 삼키지 않으면 세션 전체가 죽는다.
                Log.Print(
                    Log.LogLevel.Error,
                    $"[ServerPacketProcessor] Handler 가 예외로 실패했다. eventId={(int)context.EventId}, senderId={context.SenderId}, error={e}",
                    Log.LogFilter.Server);

                return Count(PacketBlockReason.HandlerFailed, out blockReason);
            }

            if (0 == result.Count)
            {
                return Count(PacketBlockReason.HandlerRejected, out blockReason);
            }

            for (var i = 0; i < result.Count; ++i)
            {
                PacketSendDirective directive = result[i];

                if (true == directive.IsValid && false == (_maxPayloadBytes < directive.Payload.Length))
                {
                    continue;
                }

                Log.Print(
                    Log.LogLevel.Error,
                    $"[ServerPacketProcessor] Handler 가 잘못된 발신 지시를 만들어 전체를 차단했다. eventId={(int)context.EventId}, directiveEventId={(int)directive.EventId}, targetType={(int)directive.Target.Type}, size={directive.Payload.Length}",
                    Log.LogFilter.Server);

                return Count(PacketBlockReason.InvalidDirective, out blockReason);
            }

            blockReason = PacketBlockReason.None;
            return result;
        }

        protected override void SafeDispose()
        {
            _handlers.Clear();
            base.SafeDispose();
        }

        private PacketProcessResult Reject(in PacketProcessContext context, PacketBlockReason reason, out PacketBlockReason blockReason)
        {
            // 외부 Client 가 패킷 수만큼 반복 유발할 수 있는 차단이라 Log.Print 를 쓰지 않는다. Log.Dev 는 빌드에서 인자 평가까지 제거된다.
            Log.Dev($"[ServerPacketProcessor] 패킷을 차단했다. reason={reason}, eventId={(int)context.EventId}, packetType={(int)context.PacketType}, senderId={context.SenderId}", Log.LogLevel.Warning);

            return Count(reason, out blockReason);
        }

        private PacketProcessResult Count(PacketBlockReason reason, out PacketBlockReason blockReason)
        {
            ++_blockedCounts[(int)reason];
            blockReason = reason;

            return PacketProcessResult.Block;
        }

        private void ThrowIfDisposed()
        {
            if (true == IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ServerPacketProcessor));
            }
        }
    }
}
