using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;

namespace O2un.Core.Network.Tests
{
    public sealed class P2PCommonContractTests
    {
        private sealed class TestPayload
        {
            public int SampleValue { get; set; }
            public string DisplayName { get; set; }
        }

        private sealed class TestMessenger : NetworkMessengerBase<string, ReadOnlyMemory<byte>>
        {
            private readonly bool _routeMismatch;

            public TestMessenger(bool routeMismatch)
            {
                _routeMismatch = routeMismatch;
            }

            protected override bool RoutesMismatchedResponse => _routeMismatch;

            protected override bool IsEventIdValid(string eventId)
            {
                return false == string.IsNullOrEmpty(eventId);
            }

            protected override T Deserialize<T>(ReadOnlyMemory<byte> payload)
            {
                return NetworkJson.Deserialize<T>(payload.Span);
            }

            // 기반 클래스 표면은 protected 라, 테스트에서 직접 두드리려면 얇은 통로가 필요하다.
            public Observable<T> ObserveForTest<T>(string eventId)
            {
                return ObserveEvent<T>(eventId);
            }

            public NetworkDispatchResult DispatchForTest(ulong sequence, string eventId, ReadOnlyMemory<byte> payload)
            {
                return Dispatch(sequence, eventId, payload);
            }

            public UniTask<TResponse> RequestForTest<TResponse>(string responseEventId, TimeSpan timeout, Func<ulong, CancellationToken, UniTask<bool>> sendAsync, CancellationToken ct)
            {
                return RequestAsync<TResponse>(responseEventId, timeout, sendAsync, ct);
            }
        }

        private static NetworkRequestTracker<NetworkPacketId, ReadOnlyMemory<byte>> CreateRequestTracker()
        {
            return new NetworkRequestTracker<NetworkPacketId, ReadOnlyMemory<byte>>(EqualityComparer<NetworkPacketId>.Default);
        }

        private static UniTask<TestPayload> RequestWithoutResponse(TestMessenger messenger, string responseEventId, Action<ulong> onSequenceCreated, CancellationToken ct)
        {
            return messenger.RequestForTest<TestPayload>(
                responseEventId,
                TimeSpan.FromMinutes(1),
                (sequence, _) =>
                {
                    onSequenceCreated(sequence);
                    return UniTask.FromResult(true);
                },
                ct);
        }

        private static UniTask<TestPayload> CreateWaitTask(NetworkRequestTracker<NetworkPacketId, ReadOnlyMemory<byte>> tracker, ulong sequence, NetworkPacketId expectedEventId, CancellationToken ct = default)
        {
            return tracker.CreateWaitTask(sequence, expectedEventId, payload => NetworkJson.Deserialize<TestPayload>(payload.Span), ct);
        }

        private sealed class FakeP2PTransport : SafeDisposableClass, IP2PTransport
        {
            private readonly ReactiveProperty<bool> _isConnected = new(true);
            private readonly Subject<P2PInboundPacket> _packetReceived = new();
            private readonly List<P2PRequestPacket> _sentPackets = new();
            private readonly List<CancellationTokenRegistration> _sendRegistrations = new();
            private int _sendCancellationCount;

            public ReadOnlyReactiveProperty<bool> IsConnected => _isConnected;
            public Observable<P2PInboundPacket> PacketReceived => _packetReceived;
            public IReadOnlyList<P2PRequestPacket> SentPackets => _sentPackets;
            public bool BlockSendUntilCanceled { get; set; }
            public bool WasSendCanceled => 0 < Volatile.Read(ref _sendCancellationCount);

            public UniTask<bool> SendAsync(P2PRequestPacket packet, CancellationToken ct = default)
            {
                ct.ThrowIfCancellationRequested();
                _sentPackets.Add(packet);

                if (true == BlockSendUntilCanceled)
                {
                    var source = new UniTaskCompletionSource<bool>();
                    CancellationTokenRegistration registration = ct.Register(() =>
                    {
                        Interlocked.Increment(ref _sendCancellationCount);
                        source.TrySetCanceled();
                    });
                    _sendRegistrations.Add(registration);
                    return source.Task;
                }

                return UniTask.FromResult(true);
            }

            public void Receive(P2PInboundPacket packet)
            {
                _packetReceived.OnNext(packet);
            }

            protected override void SafeDispose()
            {
                foreach (CancellationTokenRegistration registration in _sendRegistrations)
                {
                    registration.Dispose();
                }

                _packetReceived.Dispose();
                _isConnected.Dispose();
                _sendRegistrations.Clear();
                _sentPackets.Clear();
            }
        }

        [Test]
        public void NetworkJsonUsesUtf8CamelCaseRoundTrip()
        {
            var source = new TestPayload
            {
                SampleValue = 42,
                DisplayName = "한글",
            };

            byte[] payload = NetworkJson.SerializeToUtf8Bytes(source);
            string json = Encoding.UTF8.GetString(payload);
            TestPayload restored = NetworkJson.Deserialize<TestPayload>(payload);

            Assert.That(json, Does.Contain("\"sampleValue\":42"));
            Assert.That(json, Does.Contain("\"displayName\":"));
            Assert.That(json, Does.Not.Contain("\"SampleValue\""));
            Assert.That(restored.SampleValue, Is.EqualTo(source.SampleValue));
            Assert.That(restored.DisplayName, Is.EqualTo(source.DisplayName));
        }

        [Test]
        public void WireEnumValuesRemainFixed()
        {
            Assert.That((int)NetworkPacketId.None, Is.EqualTo(0));
            Assert.That((int)NetworkPacketId.DebugRelayCheckPing, Is.EqualTo(1000001));
            Assert.That((int)NetworkPacketType.None, Is.EqualTo(0));
            Assert.That((int)NetworkPacketType.Broadcast, Is.EqualTo(1));
            Assert.That((int)NetworkPacketType.Echo, Is.EqualTo(2));
        }

        [Test]
        public async Task SameSequenceAndEventIdCompletesWaiter()
        {
            using var tracker = CreateRequestTracker();
            UniTask<TestPayload> waitTask = CreateWaitTask(tracker, 1, NetworkPacketId.DebugRelayCheckPing);
            byte[] payload = NetworkJson.SerializeToUtf8Bytes(new TestPayload { SampleValue = 7 });

            bool isCompleted = tracker.TryCompleteTask(1, NetworkPacketId.DebugRelayCheckPing, payload, out bool hasWaiter, out _);
            TestPayload result = await waitTask;

            Assert.That(isCompleted, Is.True);
            Assert.That(hasWaiter, Is.True);
            Assert.That(result.SampleValue, Is.EqualTo(7));
            Assert.That(tracker.PendingCount, Is.Zero);
        }

        [Test]
        public void MismatchedEventIdKeepsWaiterPending()
        {
            using var tracker = CreateRequestTracker();
            UniTask<TestPayload> waitTask = CreateWaitTask(tracker, 2, NetworkPacketId.DebugRelayCheckPing);
            byte[] payload = NetworkJson.SerializeToUtf8Bytes(new TestPayload { SampleValue = 8 });

            bool isCompleted = tracker.TryCompleteTask(2, (NetworkPacketId)999998, payload, out bool hasWaiter, out _);

            Assert.That(isCompleted, Is.False);
            Assert.That(hasWaiter, Is.True);
            Assert.That(tracker.PendingCount, Is.EqualTo(1));

            tracker.Dispose();
            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
        }

        [Test]
        public void MessengerRoutesMismatchedResponseWhenRoutingEnabled()
        {
            using var messenger = new TestMessenger(true);
            using var cts = new CancellationTokenSource();
            int routedValue = 0;
            using IDisposable subscription = messenger.ObserveForTest<TestPayload>("other").Subscribe(payload => routedValue = payload.SampleValue);
            ulong sequence = 0;
            UniTask<TestPayload> waitTask = RequestWithoutResponse(messenger, "expected", createdSequence => sequence = createdSequence, cts.Token);

            NetworkDispatchResult result = messenger.DispatchForTest(sequence, "other", NetworkJson.SerializeToUtf8Bytes(new TestPayload { SampleValue = 12 }));

            Assert.That(result, Is.EqualTo(NetworkDispatchResult.ResponseEventMismatch));
            Assert.That(waitTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(routedValue, Is.EqualTo(12));

            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
        }

        [Test]
        public void MessengerDropsMismatchedResponseWhenRoutingDisabled()
        {
            using var messenger = new TestMessenger(false);
            using var cts = new CancellationTokenSource();
            int routedCount = 0;
            using IDisposable subscription = messenger.ObserveForTest<TestPayload>("other").Subscribe(_ => routedCount++);
            ulong sequence = 0;
            UniTask<TestPayload> waitTask = RequestWithoutResponse(messenger, "expected", createdSequence => sequence = createdSequence, cts.Token);

            NetworkDispatchResult result = messenger.DispatchForTest(sequence, "other", NetworkJson.SerializeToUtf8Bytes(new TestPayload()));

            Assert.That(result, Is.EqualTo(NetworkDispatchResult.ResponseEventMismatch));
            Assert.That(waitTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(routedCount, Is.Zero);

            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
        }

        [Test]
        public async Task PacketTypeDoesNotAffectSequenceAndEventCorrelation()
        {
            using var transport = new FakeP2PTransport();
            using var messenger = new P2PMessenger(transport);
            UniTask<TestPayload> waitTask = messenger.SendDataAndWaitAsync<TestPayload, TestPayload>(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Broadcast,
                new TestPayload { SampleValue = 1 },
                TimeSpan.FromSeconds(1));

            P2PRequestPacket request = transport.SentPackets[0];
            transport.Receive(new P2PInboundPacket(
                request.EventId,
                NetworkPacketType.Echo,
                new P2PPeerId(10),
                request.Sequence,
                NetworkJson.SerializeToUtf8Bytes(new TestPayload { SampleValue = 9 })));

            TestPayload response = await waitTask;

            Assert.That(request.PacketType, Is.EqualTo(NetworkPacketType.Broadcast));
            Assert.That(response.SampleValue, Is.EqualTo(9));
        }

        [Test]
        public void DuplicateSequenceIsRejected()
        {
            using var tracker = CreateRequestTracker();
            UniTask<TestPayload> firstTask = CreateWaitTask(tracker, 3, NetworkPacketId.DebugRelayCheckPing);

            Assert.Throws<InvalidOperationException>(() =>
                CreateWaitTask(tracker, 3, NetworkPacketId.DebugRelayCheckPing));
            Assert.That(tracker.PendingCount, Is.EqualTo(1));

            tracker.Dispose();
            Assert.CatchAsync<OperationCanceledException>(async () => await firstTask);
        }

        [Test]
        public void DisposedTrackerRejectsBeforeCreatingWaiterResources()
        {
            var tracker = CreateRequestTracker();
            tracker.Dispose();

            ObjectDisposedException exception = Assert.Throws<ObjectDisposedException>(() =>
                CreateWaitTask(tracker, 30, NetworkPacketId.DebugRelayCheckPing));

            Assert.That(exception.ObjectName, Is.EqualTo(nameof(NetworkRequestTracker<NetworkPacketId, ReadOnlyMemory<byte>>)));
            Assert.That(tracker.PendingCount, Is.Zero);
        }

        [Test]
        public void ExternalCancellationDuringSendRemainsOperationCanceled()
        {
            using var transport = new FakeP2PTransport { BlockSendUntilCanceled = true };
            using var messenger = new P2PMessenger(transport);
            using var cts = new CancellationTokenSource();
            UniTask<TestPayload> waitTask = messenger.SendDataAndWaitAsync<TestPayload, TestPayload>(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                new TestPayload(),
                TimeSpan.FromSeconds(10),
                cts.Token);

            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
            Assert.That(transport.WasSendCanceled, Is.True);
        }

        [Test]
        public void DisposeDuringBlockedSendCancelsWholeOperation()
        {
            using var transport = new FakeP2PTransport { BlockSendUntilCanceled = true };
            using var messenger = new P2PMessenger(transport);
            var stopwatch = Stopwatch.StartNew();
            UniTask<TestPayload> waitTask = messenger.SendDataAndWaitAsync<TestPayload, TestPayload>(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                new TestPayload(),
                TimeSpan.FromSeconds(10));

            messenger.Dispose();
            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
            stopwatch.Stop();

            Assert.That(transport.WasSendCanceled, Is.True);
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void CancellationTokenCancelsAndRemovesWaiter()
        {
            using var tracker = CreateRequestTracker();
            using var cts = new CancellationTokenSource();
            UniTask<TestPayload> waitTask = CreateWaitTask(tracker, 5, NetworkPacketId.DebugRelayCheckPing, cts.Token);

            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
            Assert.That(tracker.PendingCount, Is.Zero);
        }

        [Test]
        public void AlreadyCanceledTokenIsSafeDuringImmediateRegistration()
        {
            using var tracker = CreateRequestTracker();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            UniTask<TestPayload> waitTask = default;

            Assert.DoesNotThrow(() =>
                waitTask = CreateWaitTask(tracker, 6, NetworkPacketId.DebugRelayCheckPing, cts.Token));
            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
            Assert.That(tracker.PendingCount, Is.Zero);
        }

        [Test]
        public void DisposeCancelsAndClearsWaiters()
        {
            var tracker = CreateRequestTracker();
            UniTask<TestPayload> waitTask = CreateWaitTask(tracker, 7, NetworkPacketId.DebugRelayCheckPing);

            tracker.Dispose();

            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
            Assert.That(tracker.PendingCount, Is.Zero);
        }

        [Test]
        public void InvalidEventAndPacketTypeDoNotReachTrackerOrRouter()
        {
            using var transport = new FakeP2PTransport();
            using var messenger = new P2PMessenger(transport);
            using var cts = new CancellationTokenSource();
            int routedCount = 0;
            using IDisposable subscription = messenger.Observe<TestPayload>(NetworkPacketId.DebugRelayCheckPing).Subscribe(_ => routedCount++);
            UniTask<TestPayload> waitTask = messenger.SendDataAndWaitAsync<TestPayload, TestPayload>(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                new TestPayload(),
                TimeSpan.FromSeconds(1),
                cts.Token);
            P2PRequestPacket request = transport.SentPackets[0];
            byte[] payload = NetworkJson.SerializeToUtf8Bytes(new TestPayload { SampleValue = 11 });

            transport.Receive(new P2PInboundPacket(NetworkPacketId.None, NetworkPacketType.Echo, new P2PPeerId(1), request.Sequence, payload));
            transport.Receive(new P2PInboundPacket((NetworkPacketId)999998, NetworkPacketType.Echo, new P2PPeerId(1), request.Sequence, payload));
            transport.Receive(new P2PInboundPacket(request.EventId, NetworkPacketType.None, new P2PPeerId(1), request.Sequence, payload));
            transport.Receive(new P2PInboundPacket(request.EventId, (NetworkPacketType)999, new P2PPeerId(1), request.Sequence, payload));

            Assert.That(waitTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(routedCount, Is.Zero);

            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await waitTask);
        }

        [Test]
        public void TargetsRequireExplicitValidSelection()
        {
            Assert.That(P2PPacketTarget.All.IsValid, Is.True);
            Assert.That(P2PPacketTarget.ExcludeSender.IsValid, Is.True);
            Assert.That(P2PPacketTarget.SenderOnly.IsValid, Is.True);
            Assert.That(default(P2PPacketTarget).IsValid, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => P2PPacketTarget.SpecificPeer(P2PPeerId.None));

            P2PPacketTarget specificPeer = P2PPacketTarget.SpecificPeer(new P2PPeerId(77));
            Assert.That(specificPeer.IsValid, Is.True);
            Assert.That(specificPeer.PeerId, Is.EqualTo(new P2PPeerId(77)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new P2POutboundPacket(
                    NetworkPacketId.DebugRelayCheckPing,
                    NetworkPacketType.Broadcast,
                    new P2PPeerId(1),
                    0,
                    ReadOnlyMemory<byte>.Empty,
                    default));
        }

        [Test]
        public void PublicP2PBoundaryDoesNotLeakTransportSpecificTypes()
        {
            Type[] contractTypes =
            {
                typeof(NetworkPacketId),
                typeof(NetworkPacketIdExtensions),
                typeof(NetworkPacketType),
                typeof(NetworkPacketTypeExtensions),
                typeof(P2PPeerId),
                typeof(P2PPacketTargetType),
                typeof(P2PPacketTarget),
                typeof(P2PRequestPacket),
                typeof(P2PInboundPacket),
                typeof(P2POutboundPacket),
                typeof(IP2PTransport),
                typeof(IP2PMessenger),
                typeof(P2PMessenger),
                typeof(NetworkDispatchResult),
                typeof(NetworkMessengerBase<NetworkPacketId, ReadOnlyMemory<byte>>),
                typeof(NetworkRouter<NetworkPacketId, ReadOnlyMemory<byte>>),
                typeof(NetworkRequestTracker<NetworkPacketId, ReadOnlyMemory<byte>>),
                typeof(INetworkEventModule),
                typeof(NetworkJson),
                typeof(NetworkUtils),
            };

            foreach (Type contractType in contractTypes)
            {
                AssertPublicMemberTypesAreTransportNeutral(contractType);
            }
        }

        private static void AssertPublicMemberTypesAreTransportNeutral(Type contractType)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
            var visited = new HashSet<Type>();

            AssertTransportNeutral(contractType, visited);

            foreach (PropertyInfo property in contractType.GetProperties(FLAGS))
            {
                AssertTransportNeutral(property.PropertyType, visited);
            }

            foreach (FieldInfo field in contractType.GetFields(FLAGS))
            {
                AssertTransportNeutral(field.FieldType, visited);
            }

            foreach (ConstructorInfo constructor in contractType.GetConstructors(FLAGS))
            {
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    AssertTransportNeutral(parameter.ParameterType, visited);
                }
            }

            foreach (MethodInfo method in contractType.GetMethods(FLAGS))
            {
                AssertTransportNeutral(method.ReturnType, visited);

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AssertTransportNeutral(parameter.ParameterType, visited);
                }
            }
        }

        private static void AssertTransportNeutral(Type type, HashSet<Type> visited)
        {
            if (null == type || false == visited.Add(type))
            {
                return;
            }

            if (true == type.HasElementType)
            {
                AssertTransportNeutral(type.GetElementType(), visited);
            }

            if (true == type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    AssertTransportNeutral(argument, visited);
                }
            }

            string typeNamespace = type.Namespace ?? string.Empty;
            Assert.That(typeNamespace.StartsWith("Unity.NetCode", StringComparison.Ordinal), Is.False, type.FullName);
            Assert.That(typeNamespace.StartsWith("Unity.Entities", StringComparison.Ordinal), Is.False, type.FullName);
            Assert.That(typeNamespace.StartsWith("Unity.Networking.Transport", StringComparison.Ordinal), Is.False, type.FullName);
            Assert.That(typeNamespace.StartsWith("System.Net.WebSockets", StringComparison.Ordinal), Is.False, type.FullName);
            Assert.That(type.Name, Is.Not.EqualTo("NetworkId"), type.FullName);
            Assert.That(type.Name, Does.Not.Contain("WebSocket"), type.FullName);
        }
    }
}
