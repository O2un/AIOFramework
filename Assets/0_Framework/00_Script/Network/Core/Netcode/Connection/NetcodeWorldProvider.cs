using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    public sealed class NetcodeWorldProvider
    {
        public const string SERVER_WORLD_NAME = "O2unServerWorld";
        public const string CLIENT_WORLD_NAME = "O2unClientWorld";

        public World GetServerWorld() => Find(WorldFlags.GameServer);
        public World GetClientWorld() => Find(WorldFlags.GameClient);

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

        private static World Find(WorldFlags flags)
        {
            foreach (var world in World.All)
            {
                if (false == world.IsCreated)
                {
                    continue;
                }

                if (0 != (world.Flags & flags))
                {
                    return world;
                }
            }

            return null;
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
