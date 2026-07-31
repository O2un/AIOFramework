using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    public sealed class NetcodeWorldProvider
    {
        public const string SERVER_WORLD_NAME = "O2unServerWorld";
        public const string CLIENT_WORLD_NAME = "O2unClientWorld";

        public World GetServerWorld() => ClientServerBootstrap.ServerWorld;
        public World GetClientWorld() => ClientServerBootstrap.ClientWorld;

        public World CreateServerWorld(string name)
        {
            var existing = GetServerWorld();

            if (null != existing)
            {
                return existing;
            }

            return ClientServerBootstrap.CreateServerWorld(name);
        }

        public World CreateClientWorld(string name)
        {
            var existing = GetClientWorld();

            if (null != existing)
            {
                return existing;
            }

            return ClientServerBootstrap.CreateClientWorld(name);
        }

        public void DisposeClientWorld() => Dispose(GetClientWorld());
        public void DisposeServerWorld() => Dispose(GetServerWorld());

        public void DisposeAll()
        {
            DisposeClientWorld();
            DisposeServerWorld();
        }

        private static void Dispose(World world)
        {
            if (null == world || false == world.IsCreated)
            {
                return;
            }

            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
            world.Dispose();
        }
    }
}
