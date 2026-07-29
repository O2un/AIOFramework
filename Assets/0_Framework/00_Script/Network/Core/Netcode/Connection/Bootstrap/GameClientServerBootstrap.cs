using System;
using O2un.Utils;
using Unity.NetCode;
using UnityEngine;

namespace O2un.Core.Network
{
    /// <summary>
    /// 데디케이티드 전용 진입점. 프로세스 실행 모드 판단과 CLI 설정, ServerWorld 생성까지만 맡는다.
    /// Listen 은 <see cref="DedicatedServerConnectionModule"/> 이 담당한다.
    /// </summary>
    public sealed class GameClientServerBootstrap : ClientServerBootstrap
    {
        public static DedicatedServerArguments DedicatedArguments { get; private set; }

        public override bool Initialize(string defaultWorldName)
        {
            Application.runInBackground = true;

            if (false == IsDedicatedServer())
            {
                // false 는 "기본 World 생성을 Unity 에게 맡긴다"는 뜻이다. true 를 주면 여기서 World 를
                // 만들지 않은 채 책임만 가져가 기본 World 가 통째로 사라진다.
                return false;
            }

            DedicatedArguments = DedicatedServerArguments.Parse(Environment.GetCommandLineArgs());

            CreateServerWorld($"{defaultWorldName}-Server");

            return true;
        }

        private static bool IsDedicatedServer()
        {
#if UNITY_SERVER
            return true;
#else
            return CommandLine.HasArgument("-server");
#endif
        }
    }
}
