using System;
using System.Collections.Generic;
using O2un.Core.Utils;

namespace O2un.Core.Network
{
    /// <summary>
    /// ECS Server World 에는 DI 가 없어 기능 Module 이 Handler 를 주입할 수 없다. 그래서 남은 통로가 정적 설치자다.
    /// </summary>
    public static class ServerPacketHandlers
    {
        private static readonly List<Action<IHostPacketHandlerRegistry>> _installers = new();
        private static readonly List<IHostPacketHandlerRegistry> _registries = new();

        public static void AddInstaller(Action<IHostPacketHandlerRegistry> installer)
        {
            if (null == installer)
            {
                throw new ArgumentNullException(nameof(installer));
            }

            if (true == _installers.Contains(installer))
            {
                return;
            }

            _installers.Add(installer);

            // Dedicated Server 는 Server World 를 먼저 만든 뒤 기능 Module 을 준비한다.
            // 일회성 이벤트로 두면 그 순서에서 Registry 가 영원히 빈 채로 고정된다.
            foreach (IHostPacketHandlerRegistry registry in _registries.ToArray())
            {
                Install(installer, registry);
            }
        }

        public static void RemoveInstaller(Action<IHostPacketHandlerRegistry> installer)
        {
            _installers.Remove(installer);
        }

        internal static void AttachRegistry(IHostPacketHandlerRegistry registry)
        {
            if (null == registry || true == _registries.Contains(registry))
            {
                return;
            }

            _registries.Add(registry);

            foreach (Action<IHostPacketHandlerRegistry> installer in _installers.ToArray())
            {
                Install(installer, registry);
            }
        }

        internal static void DetachRegistry(IHostPacketHandlerRegistry registry)
        {
            _registries.Remove(registry);
        }

        private static void Install(Action<IHostPacketHandlerRegistry> installer, IHostPacketHandlerRegistry registry)
        {
            try
            {
                installer.Invoke(registry);
            }
            catch (Exception e)
            {
                // 한 Module 의 설치 실패가 남은 Module 의 Handler 까지 없애면 원인 없이 전체가 fail-closed 로 막힌다.
                Log.Print(Log.LogLevel.Error, $"[ServerPacketHandlers] Handler 설치에 실패했다. error={e.Message}", Log.LogFilter.Server);
            }
        }
    }
}
