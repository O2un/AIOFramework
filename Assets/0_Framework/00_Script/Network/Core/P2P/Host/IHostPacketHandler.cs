namespace O2un.Core.Network
{
    /// <summary>
    /// Host 에서 Event 하나의 유효성·권한·최종 논리 대상을 판정한다. Relay 여부를 결정하는 유일한 지점이다.
    /// </summary>
    public interface IHostPacketHandler
    {
        NetworkPacketId EventId { get; }

        bool IsAllowedPacketType(NetworkPacketType packetType);

        PacketProcessResult Handle(in PacketProcessContext context);
    }

    /// <summary>
    /// 계산 Event 를 ECS Job 으로 넘기기 위한 확장 지점이다. 소비자는 아직 없고 스케줄은 후속 단위에서 채운다.
    /// </summary>
    public interface IHostPacketJobHandler : IHostPacketHandler
    {
        bool WantsJobExecution(in PacketProcessContext context);
    }

    public interface IHostPacketHandlerRegistry
    {
        void Register(IHostPacketHandler handler);
        bool Unregister(NetworkPacketId eventId);
    }
}
