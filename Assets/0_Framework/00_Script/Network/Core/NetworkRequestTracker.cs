using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.Core.Network
{
    /// <summary>
    /// 메인 스레드 전용이다. 수신은 ObserveOnMainThread 로 들어오고 기한은 플레이어 루프가 재우므로 잠금을 두지 않는다.
    /// </summary>
    public sealed class NetworkRequestTracker<TEventId, TPayload> : SafeDisposableClass
    {
        private interface IRequestWaiter
        {
            TEventId ExpectedEventId { get; }
            void Start(CancellationToken ct, Action onCanceled);
            void TrySetResult(TPayload payload);
            void TrySetCanceled();
            void Dispose();
        }

        private sealed class RequestWaiter<T> : IRequestWaiter
        {
            private readonly UniTaskCompletionSource<T> _source = new();
            private readonly Func<TPayload, T> _parser;
            private CancellationTokenRegistration _registration;

            public RequestWaiter(TEventId expectedEventId, Func<TPayload, T> parser)
            {
                ExpectedEventId = expectedEventId;
                _parser = parser;
            }

            public TEventId ExpectedEventId { get; }
            public UniTask<T> Task => _source.Task;

            public void Start(CancellationToken ct, Action onCanceled)
            {
                _registration = ct.Register(onCanceled);
            }

            public void TrySetResult(TPayload payload)
            {
                try
                {
                    _source.TrySetResult(_parser(payload));
                }
                catch (Exception ex)
                {
                    _source.TrySetException(ex);
                }
            }

            public void TrySetCanceled()
            {
                _source.TrySetCanceled();
            }

            public void Dispose()
            {
                _registration.Dispose();
                _registration = default;
            }
        }

        private readonly IEqualityComparer<TEventId> _eventComparer;
        private readonly Dictionary<ulong, IRequestWaiter> _waiters = new();
        private ulong _nextSequence;

        public NetworkRequestTracker(IEqualityComparer<TEventId> eventComparer)
        {
            _eventComparer = eventComparer ?? EqualityComparer<TEventId>.Default;
        }

        public int PendingCount => _waiters.Count;

        public ulong CreateSequence()
        {
            ThrowIfDisposed();
            _nextSequence++;
            if (0 == _nextSequence)
            {
                _nextSequence = 1;
            }

            return _nextSequence;
        }

        public UniTask<T> CreateWaitTask<T>(ulong sequence, TEventId expectedEventId, Func<TPayload, T> parser, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (0 == sequence)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (null == parser)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            if (true == _waiters.ContainsKey(sequence))
            {
                throw new InvalidOperationException($"이미 등록된 네트워크 Sequence다. sequence={sequence}");
            }

            var waiter = new RequestWaiter<T>(expectedEventId, parser);
            _waiters.Add(sequence, waiter);

            waiter.Start(ct, () => CancelWaiter(sequence, waiter));
            return waiter.Task;
        }

        public bool TryCompleteTask(ulong sequence, TEventId eventId, TPayload payload, out bool hasWaiter, out TEventId expectedEventId)
        {
            if (false == _waiters.TryGetValue(sequence, out IRequestWaiter waiter))
            {
                hasWaiter = false;
                expectedEventId = default;
                return false;
            }

            hasWaiter = true;
            expectedEventId = waiter.ExpectedEventId;
            if (false == _eventComparer.Equals(waiter.ExpectedEventId, eventId))
            {
                return false;
            }

            _waiters.Remove(sequence);
            waiter.TrySetResult(payload);
            waiter.Dispose();
            return true;
        }

        public void CancelTask(ulong sequence)
        {
            if (false == _waiters.TryGetValue(sequence, out IRequestWaiter waiter))
            {
                return;
            }

            _waiters.Remove(sequence);
            waiter.TrySetCanceled();
            waiter.Dispose();
        }

        public void Clear()
        {
            var waiters = new IRequestWaiter[_waiters.Count];
            _waiters.Values.CopyTo(waiters, 0);
            _waiters.Clear();

            foreach (var waiter in waiters)
            {
                waiter.TrySetCanceled();
                waiter.Dispose();
            }
        }

        protected override void SafeDispose()
        {
            Clear();
        }

        private void CancelWaiter(ulong sequence, IRequestWaiter expectedWaiter)
        {
            if (false == _waiters.TryGetValue(sequence, out IRequestWaiter waiter) || false == ReferenceEquals(waiter, expectedWaiter))
            {
                return;
            }

            _waiters.Remove(sequence);
            expectedWaiter.TrySetCanceled();
            expectedWaiter.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (true == IsDisposed)
            {
                throw new ObjectDisposedException(nameof(NetworkRequestTracker<TEventId, TPayload>));
            }
        }
    }
}
