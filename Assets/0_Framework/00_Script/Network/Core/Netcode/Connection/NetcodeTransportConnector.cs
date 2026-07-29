using System;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;

namespace O2un.Core.Network
{
    public sealed class NetcodeTransportConnector
    {
        public const string ANY_ADDRESS = "0.0.0.0";
        public const string LOOPBACK = "127.0.0.1";

        public void Listen(World serverWorld, string bindAddress, ushort port)
        {
            Request(serverWorld, nameof(Listen), entityManager =>
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new NetworkStreamRequestListen
                {
                    Endpoint = CreateListenEndpoint(bindAddress, port),
                });
            });
        }

        public void Connect(World clientWorld, string address, ushort port)
        {
            Request(clientWorld, nameof(Connect), entityManager =>
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new NetworkStreamRequestConnect
                {
                    Endpoint = NetworkEndpoint.Parse(address, port),
                });
            });
        }

        private static void Request(World world, string operation, Action<EntityManager> build)
        {
            // 조용히 넘기면 "접속했는데 아무 일도 안 일어남"이 되므로 여기서 끊는다.
            if (null == world || false == world.IsCreated)
            {
                throw new InvalidOperationException($"[NetcodeTransportConnector] World 가 생성되지 않았다. operation={operation}");
            }

            build(world.EntityManager);
        }

        private static NetworkEndpoint CreateListenEndpoint(string address, ushort port)
        {
            if (true == string.IsNullOrWhiteSpace(address) || ANY_ADDRESS == address)
            {
                return NetworkEndpoint.AnyIpv4.WithPort(port);
            }

            return NetworkEndpoint.Parse(address, port);
        }
    }
}
