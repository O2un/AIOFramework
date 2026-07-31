using System.Collections.Generic;
using O2un.Core.Utils;
using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateAfter(typeof(ServerPacketProcessSystem))]
    internal partial class ServerPacketExportSystem : SystemBase
    {
        // int 로 줄이면 상위 비트만 다른 Peer 가 같은 키로 접혀 다른 연결에 오발신한다.
        private readonly Dictionary<P2PPeerId, Entity> _connections = new();

        private Entity _queueEntity;

        protected override void OnCreate()
        {
            _queueEntity = ServerPacketQueueUtility.GetOrCreate(EntityManager);
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<ServerPacketQueue>()));
        }

        protected override void OnUpdate()
        {
            DynamicBuffer<OutboundPacketElement> outbound = EntityManager.GetBuffer<OutboundPacketElement>(_queueEntity);

            if (0 == outbound.Length)
            {
                return;
            }

            // RPC 엔티티 생성은 구조 변경이라 연결 순회 중에 할 수 없다.
            var drained = new OutboundPacketElement[outbound.Length];

            for (var i = 0; i < outbound.Length; ++i)
            {
                drained[i] = outbound[i];
            }

            outbound.Clear();

            _connections.Clear();

            foreach ((RefRO<NetworkId> networkId, Entity entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess())
            {
                _connections[new P2PPeerId((ulong)networkId.ValueRO.Value)] = entity;
            }

            foreach (OutboundPacketElement packet in drained)
            {
                Dispatch(packet);
            }
        }

        private void Dispatch(in OutboundPacketElement packet)
        {
            var senderId = new P2PPeerId((ulong)packet.SenderNetworkId);

            switch ((P2PPacketTargetType)packet.TargetType)
            {
                case P2PPacketTargetType.All:
                    foreach (Entity connection in _connections.Values)
                    {
                        Send(packet, connection);
                    }

                    break;

                case P2PPacketTargetType.ExcludeSender:
                    foreach (KeyValuePair<P2PPeerId, Entity> connection in _connections)
                    {
                        if (senderId == connection.Key)
                        {
                            continue;
                        }

                        Send(packet, connection.Value);
                    }

                    break;

                case P2PPacketTargetType.SenderOnly:
                    SendToPeer(packet, senderId);
                    break;

                case P2PPacketTargetType.SpecificPeer:
                    SendToPeer(packet, new P2PPeerId(packet.TargetPeerId));
                    break;

                default:
                    Log.Print(
                        Log.LogLevel.Error,
                        $"[ServerPacketExportSystem] 알 수 없는 발신 대상이라 패킷을 버렸다. eventId={packet.EventId}, targetType={packet.TargetType}",
                        Log.LogFilter.Server);
                    break;
            }
        }

        private void SendToPeer(in OutboundPacketElement packet, P2PPeerId peerId)
        {
            if (false == _connections.TryGetValue(peerId, out Entity connection))
            {
                Log.Dev($"[ServerPacketExportSystem] 발신 대상 연결이 없어 패킷을 버렸다. eventId={packet.EventId}, peerId={peerId}", Log.LogLevel.Warning);
                return;
            }

            Send(packet, connection);
        }

        private void Send(in OutboundPacketElement packet, Entity connection)
        {
            var rpcEntity = EntityManager.CreateEntity();

            EntityManager.AddComponentData(rpcEntity, new RelayPacketRpc
            {
                EventId = packet.EventId,
                PacketType = packet.PacketType,
                SenderNetworkId = packet.SenderNetworkId,
                Sequence = packet.Sequence,
                Payload = packet.Payload,
            });
            EntityManager.AddComponentData(rpcEntity, new SendRpcCommandRequest { TargetConnection = connection });
        }
    }
}
