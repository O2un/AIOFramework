using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using O2un.Core;
using O2un.Core.Data;
using O2un.Core.Network.Editor;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace O2un.Core.Network.Tests
{
    public sealed class WssSessionLifecycleTests
    {
        private const string SAMPLE_BUILD_ID = "20260731120000-abcdef01";
        private const string OTHER_BUILD_ID = "20260731120000-abcdef02";

        private sealed class FakeRuntimeDataProvider : IRuntimeDataProvider
        {
            public T Get<T>() where T : RuntimeData<T>, new() => new();
        }

        private sealed class FakeNetworkMessenger : INetworkMessenger
        {
            private readonly ReactiveProperty<bool> _isConnected = new(true);

            public object NextAck { get; set; }
            public object LastRequest { get; private set; }

            public ReadOnlyReactiveProperty<bool> IsConnected => _isConnected;

            public Observable<T> Observe<T>(string eventName) => Observable.Never<T>();
            public Observable<T> Observe<T>(string eventName, Func<JsonElement, T> parser) => Observable.Never<T>();

            public UniTask<bool> SendDataAsync<T>(string eventName, T data, CancellationToken ct = default) => UniTask.FromResult(true);

            public UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(string eventName, TRequest data, CancellationToken ct = default) where TResponse : class
            {
                LastRequest = data;
                return UniTask.FromResult(NextAck as TResponse);
            }
        }

        private sealed class FakeTransport : SafeDisposableClass, IP2PTransport
        {
            private readonly ReactiveProperty<bool> _isConnected = new(true);
            private readonly Subject<P2PInboundPacket> _packetReceived = new();
            private readonly List<string> _teardownLog;

            public FakeTransport(List<string> teardownLog)
            {
                _teardownLog = teardownLog;
            }

            public ReadOnlyReactiveProperty<bool> IsConnected => _isConnected;
            public Observable<P2PInboundPacket> PacketReceived => _packetReceived;
            public int SentCount { get; private set; }

            public UniTask<bool> SendAsync(P2PRequestPacket packet, CancellationToken ct = default)
            {
                ++SentCount;
                return UniTask.FromResult(true);
            }

            protected override void SafeDispose()
            {
                _teardownLog?.Add("transport");
                _packetReceived.Dispose();
                _isConnected.Dispose();

                base.SafeDispose();
            }
        }

        private static WebSocketMatchmakingService CreateMatchmaking(FakeNetworkMessenger messenger)
        {
            var service = new WebSocketMatchmakingService(messenger, new FakeRuntimeDataProvider());
            service.Initialize();
            return service;
        }

        [Test]
        public void GeneratedIdCarriesUtcTimeAndRandomSuffix()
        {
            string first = BuildCompatibilityIdGenerator.Create();
            string second = BuildCompatibilityIdGenerator.Create();

            Assert.That(BuildCompatibilityId.IsValid(first), Is.True, first);
            Assert.That(BuildCompatibilityId.IsValid(second), Is.True, second);

            var stamp = DateTime.ParseExact(
                first.Substring(0, BuildCompatibilityId.TIME_LENGTH),
                BuildCompatibilityId.TIME_FORMAT,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

            Assert.That((DateTime.UtcNow - stamp).Duration(), Is.LessThan(TimeSpan.FromMinutes(1)));
            Assert.That(first.Substring(BuildCompatibilityId.TIME_LENGTH + 1), Is.Not.EqualTo(second.Substring(BuildCompatibilityId.TIME_LENGTH + 1)));
        }

        [Test]
        public void RepositoryDefaultIsAnInvalidSentinelSoMissingGenerationCannotPass()
        {
            Assert.That(BuildCompatibilityIdSource.VALUE, Is.EqualTo(BuildCompatibilityId.NOT_GENERATED));
            Assert.That(BuildCompatibilityId.IsValid(BuildCompatibilityId.NOT_GENERATED), Is.False);
            Assert.That(BuildCompatibilityId.IsMatch(BuildCompatibilityId.NOT_GENERATED, BuildCompatibilityId.NOT_GENERATED), Is.False);

            string baked = BuildCompatibilityIdGenerator.Create();
            Assert.That(BuildCompatibilityIdGenerator.BuildSource(baked), Does.Contain($"\"{baked}\""));
        }

        [Test]
        public void CurrentIsReadOnlyAtRuntimeAndComesFromTheGeneratedSource()
        {
            Assert.That(BuildCompatibilityId.Current, Is.EqualTo(BuildCompatibilityIdSource.VALUE));
            Assert.That(BuildCompatibilityId.Current, Is.EqualTo(BuildCompatibilityId.Current));

            Assert.That(
                typeof(BuildCompatibilityId).GetProperty(nameof(BuildCompatibilityId.Current))?.SetMethod,
                Is.Null,
                "런타임에 Build ID 를 다시 세울 수 있으면 같은 배포 묶음이 서로 다른 값을 갖는다.");
        }

        [Test]
        public void ExactMatchIsTheOnlyCompatibilityRule()
        {
            Assert.That(BuildCompatibilityId.IsMatch(SAMPLE_BUILD_ID, SAMPLE_BUILD_ID), Is.True);
            Assert.That(BuildCompatibilityId.IsMatch(SAMPLE_BUILD_ID, OTHER_BUILD_ID), Is.False);
            Assert.That(BuildCompatibilityId.IsMatch(SAMPLE_BUILD_ID, null), Is.False);
            Assert.That(BuildCompatibilityId.IsMatch(SAMPLE_BUILD_ID, string.Empty), Is.False);
            Assert.That(BuildCompatibilityId.IsMatch(SAMPLE_BUILD_ID, SAMPLE_BUILD_ID.ToUpperInvariant()), Is.False);
            Assert.That(BuildCompatibilityId.IsValid("20261332120000-abcdef01"), Is.False);
            Assert.That(BuildCompatibilityId.IsValid("20260731120000-abcdefg1"), Is.False);
        }

        [Test]
        public async Task RoomRequestsCarryLocalBuildCompatibilityId()
        {
            var messenger = new FakeNetworkMessenger
            {
                NextAck = new JoinRoomAck { IsSuccess = false, Reason = MatchmakingReasons.ROOM_NOT_FOUND },
            };

            using WebSocketMatchmakingService service = CreateMatchmaking(messenger);

            await service.JoinRoomAsync("player", "AAAA");

            Assert.That(((JoinRoomReq)messenger.LastRequest).BuildCompatibilityId, Is.EqualTo(BuildCompatibilityId.Current));

            messenger.NextAck = new CreateRoomAck { IsSuccess = false, Reason = MatchmakingReasons.ROOM_FULL };

            await service.CreateRoomAsync("player", 4);

            Assert.That(((CreateRoomReq)messenger.LastRequest).BuildCompatibilityId, Is.EqualTo(BuildCompatibilityId.Current));
        }

        [Test]
        public async Task MismatchedRoomBuildIdBlocksConnectionInfo()
        {
            var messenger = new FakeNetworkMessenger
            {
                NextAck = new JoinRoomAck
                {
                    IsSuccess = true,
                    Connection = new MatchConnectionInfo { RoomCode = "AAAA", BuildCompatibilityId = OTHER_BUILD_ID },
                },
            };

            using WebSocketMatchmakingService service = CreateMatchmaking(messenger);

            var assigned = 0;
            using IDisposable subscription = service.MatchAssigned.Subscribe(_ => ++assigned);

            LogAssert.Expect(LogType.Error, new Regex("방 호환성 값이 내 빌드와 다르다"));

            MatchmakingResult<MatchConnectionInfo> result = await service.JoinRoomAsync("player", "AAAA");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Reason, Is.EqualTo(MatchmakingReasons.BUILD_INCOMPATIBLE));
            Assert.That(service.CurrentConnection, Is.Null);
            Assert.That(assigned, Is.EqualTo(0));
        }

        [Test]
        public async Task MissingRoomBuildIdIsTreatedAsMismatch()
        {
            var messenger = new FakeNetworkMessenger
            {
                NextAck = new JoinRoomAck
                {
                    IsSuccess = true,
                    Connection = new MatchConnectionInfo { RoomCode = "AAAA" },
                },
            };

            using WebSocketMatchmakingService service = CreateMatchmaking(messenger);

            LogAssert.Expect(LogType.Error, new Regex("방 호환성 값이 내 빌드와 다르다"));

            MatchmakingResult<MatchConnectionInfo> result = await service.JoinRoomAsync("player", "AAAA");

            Assert.That(result.Reason, Is.EqualTo(MatchmakingReasons.BUILD_INCOMPATIBLE));
            Assert.That(service.CurrentConnection, Is.Null);
        }

        [Test]
        public async Task MatchingRoomBuildIdProvidesConnectionInfo()
        {
            // 저장소 기본값은 sentinel 이라 이 성공 경로는 배포 묶음 ID 를 한 번 구운 작업 트리에서만 판정할 수 있다.
            Assume.That(
                BuildCompatibilityId.IsValid(BuildCompatibilityId.Current),
                $"Build Compatibility Id 가 생성되지 않았다. {nameof(BuildCompatibilityIdGenerator)}.{nameof(BuildCompatibilityIdGenerator.Generate)} 를 한 번 실행한다.");

            var messenger = new FakeNetworkMessenger
            {
                NextAck = new JoinRoomAck
                {
                    IsSuccess = true,
                    Connection = new MatchConnectionInfo { RoomCode = "AAAA", BuildCompatibilityId = BuildCompatibilityId.Current },
                },
            };

            using WebSocketMatchmakingService service = CreateMatchmaking(messenger);

            MatchmakingResult<MatchConnectionInfo> result = await service.JoinRoomAsync("player", "AAAA");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(service.CurrentConnection, Is.Not.Null);
            Assert.That(service.CurrentConnection.RoomCode, Is.EqualTo("AAAA"));
        }

        [Test]
        public async Task GenerationSkippedBuildCannotEnterAnyRoom()
        {
            var messenger = new FakeNetworkMessenger
            {
                NextAck = new JoinRoomAck
                {
                    IsSuccess = true,
                    Connection = new MatchConnectionInfo { RoomCode = "AAAA", BuildCompatibilityId = BuildCompatibilityId.NOT_GENERATED },
                },
            };

            using WebSocketMatchmakingService service = CreateMatchmaking(messenger);

            LogAssert.Expect(LogType.Error, new Regex("방 호환성 값이 내 빌드와 다르다"));

            MatchmakingResult<MatchConnectionInfo> result = await service.JoinRoomAsync("player", "AAAA");

            Assert.That(result.Reason, Is.EqualTo(MatchmakingReasons.BUILD_INCOMPATIBLE));
            Assert.That(service.CurrentConnection, Is.Null);
        }

        [Test]
        public void SessionIsAbsentBeforeMatch()
        {
            using var coordinator = new P2PSessionCoordinator(() => new FakeTransport(null));

            Assert.That(coordinator.HasSession, Is.False);
            Assert.That(coordinator.Messenger, Is.Null);
        }

        [Test]
        public void OpenSessionCreatesMessengerOverTheSessionTransport()
        {
            FakeTransport transport = null;

            using var coordinator = new P2PSessionCoordinator(() =>
            {
                transport = new FakeTransport(null);
                return transport;
            });

            Assert.That(coordinator.OpenSession(), Is.True);
            Assert.That(coordinator.HasSession, Is.True);
            Assert.That(coordinator.Messenger, Is.Not.Null);
            Assert.That(coordinator.Messenger.IsConnected.CurrentValue, Is.True);
        }

        [Test]
        public void MissingClientWorldLeavesNoSession()
        {
            using var coordinator = new P2PSessionCoordinator(() => null);

            Assert.That(coordinator.OpenSession(), Is.False);
            Assert.That(coordinator.HasSession, Is.False);
            Assert.That(coordinator.Messenger, Is.Null);
        }

        [Test]
        public void CloseSessionTearsDownMessengerBeforeTransport()
        {
            var teardownLog = new List<string>();
            FakeTransport transport = null;

            using var coordinator = new P2PSessionCoordinator(() =>
            {
                transport = new FakeTransport(teardownLog);
                return transport;
            });

            coordinator.OpenSession();
            IP2PMessenger messenger = coordinator.Messenger;

            coordinator.CloseSession();

            Assert.That(teardownLog, Is.EqualTo(new[] { "transport" }));
            Assert.That(messenger.IsDisposed, Is.True);
            Assert.That(transport.IsDisposed, Is.True);
            Assert.That(coordinator.HasSession, Is.False);
            Assert.That(coordinator.Messenger, Is.Null);
        }

        [Test]
        public void ClosedSessionRejectsFurtherSends()
        {
            FakeTransport transport = null;

            using var coordinator = new P2PSessionCoordinator(() =>
            {
                transport = new FakeTransport(null);
                return transport;
            });

            coordinator.OpenSession();
            IP2PMessenger messenger = coordinator.Messenger;

            messenger.SendDataAsync(NetworkPacketId.DebugRelayCheckPing, NetworkPacketType.Broadcast, "hello").GetAwaiter().GetResult();
            Assert.That(transport.SentCount, Is.EqualTo(1));

            coordinator.CloseSession();

            Assert.CatchAsync<ObjectDisposedException>(async () =>
                await messenger.SendDataAsync(NetworkPacketId.DebugRelayCheckPing, NetworkPacketType.Broadcast, "bye"));
            Assert.That(transport.SentCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CloseSessionCancelsPendingRequests()
        {
            using var coordinator = new P2PSessionCoordinator(() => new FakeTransport(null));

            coordinator.OpenSession();

            UniTask<string> pending = coordinator.Messenger.SendDataAndWaitAsync<string, string>(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                "ping",
                TimeSpan.FromSeconds(30));

            coordinator.CloseSession();

            try
            {
                await pending;
                Assert.Fail("세션 종료 뒤에도 대기 요청이 살아 있다.");
            }
            catch (OperationCanceledException)
            {
                Assert.Pass();
            }
        }

        [Test]
        public void NewMatchClosesPreviousSessionFirst()
        {
            var teardownLog = new List<string>();

            using var coordinator = new P2PSessionCoordinator(() => new FakeTransport(teardownLog));

            coordinator.OpenSession();
            IP2PMessenger firstMessenger = coordinator.Messenger;

            coordinator.OpenSession();

            Assert.That(teardownLog, Is.EqualTo(new[] { "transport" }));
            Assert.That(firstMessenger.IsDisposed, Is.True);
            Assert.That(coordinator.Messenger, Is.Not.SameAs(firstMessenger));
            Assert.That(coordinator.Messenger.IsDisposed, Is.False);
        }

        [Test]
        public void SessionSurvivesUntilExplicitCloseAndDisposeCleansUp()
        {
            var teardownLog = new List<string>();
            var coordinator = new P2PSessionCoordinator(() => new FakeTransport(teardownLog));

            coordinator.OpenSession();

            IP2PMessenger messenger = coordinator.Messenger;

            Assert.That(coordinator.Messenger, Is.SameAs(messenger));
            Assert.That(messenger.IsDisposed, Is.False);
            Assert.That(teardownLog, Is.Empty);

            coordinator.Dispose();

            Assert.That(teardownLog, Is.EqualTo(new[] { "transport" }));
            Assert.That(coordinator.HasSession, Is.False);
        }
    }
}
