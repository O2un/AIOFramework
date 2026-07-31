using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace O2un.Core.Network
{
    public enum NetworkDispatchResult
    {
        Routed = 0,
        ResponseCompleted = 1,
        ResponseEventMismatch = 2,
        InvalidEvent = 3,
    }

    public sealed class NetworkSendFailedException : Exception
    {
        public NetworkSendFailedException(string message) : base(message)
        {
            // NULL
        }
    }

    /// <summary>
    /// 전송 통로와 무관한 봉투 상관 관계(sequence-event 매칭, 라우팅, 요청/응답 대기)를 담는다.
    /// 전송별 차이는 파생 클래스가 채우고, 공개 계약은 파생 클래스가 각자의 인터페이스로 정한다.
    /// </summary>
    public abstract class NetworkMessengerBase<TEventId, TPayload> : SafeDisposableClass
    {
        private readonly NetworkRouter<TEventId, TPayload> _router;
        private readonly NetworkRequestTracker<TEventId, TPayload> _requestTracker;
        private readonly CancellationTokenSource _lifetimeSource;

        protected NetworkMessengerBase()
        {
            IEqualityComparer<TEventId> comparer = EqualityComparer<TEventId>.Default;
            _router = new NetworkRouter<TEventId, TPayload>(comparer);
            _requestTracker = new NetworkRequestTracker<TEventId, TPayload>(comparer);
            _lifetimeSource = new CancellationTokenSource();
            LifetimeToken = _lifetimeSource.Token;
        }

        protected CancellationToken LifetimeToken { get; }

        protected abstract bool IsEventIdValid(TEventId eventId);

        // 제네릭 메서드라 delegate 로 접을 수 없다. 파생 클래스가 직접 채워야 하는 유일한 변형 축이다.
        protected abstract T Deserialize<T>(TPayload payload);

        // 응답을 기다리는 sequence 에 다른 이벤트가 실려 온 경우, 그 패킷을 일반 구독으로 흘릴지 버릴지.
        protected virtual bool RoutesMismatchedResponse => false;

        protected Observable<T> ObserveEvent<T>(TEventId eventId)
        {
            ThrowIfDisposed();
            ThrowIfInvalidEventId(eventId);
            return _router.Observe(eventId).Select(payload => Deserialize<T>(payload));
        }

        protected Observable<T> ObserveEvent<T>(TEventId eventId, Func<TPayload, T> parser)
        {
            ThrowIfDisposed();
            ThrowIfInvalidEventId(eventId);
            if (null == parser)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            return _router.Observe(eventId).Select(parser);
        }

        protected ulong CreateSequence()
        {
            ThrowIfDisposed();
            return _requestTracker.CreateSequence();
        }

        protected async UniTask<TResponse> RequestAsync<TResponse>(TEventId responseEventId, TimeSpan timeout, Func<ulong, CancellationToken, UniTask<bool>> sendAsync, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ThrowIfInvalidEventId(responseEventId);
            if (null == sendAsync)
            {
                throw new ArgumentNullException(nameof(sendAsync));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(ct, LifetimeToken);
            CancellationToken operationToken = operationSource.Token;

            ulong sequence = _requestTracker.CreateSequence();
            UniTask<TResponse> waitTask = _requestTracker.CreateWaitTask(sequence, responseEventId, payload => Deserialize<TResponse>(payload), operationToken);

            try
            {
                bool isSent = await sendAsync(sequence, operationToken);
                if (false == isSent)
                {
                    throw new NetworkSendFailedException($"네트워크 송신에 실패했다. event={responseEventId}");
                }

                var (isCompleted, response) = await UniTask.WhenAny(waitTask, UniTask.Delay(timeout, cancellationToken: operationToken));
                if (false == isCompleted)
                {
                    throw new TimeoutException($"네트워크 응답 제한 시간을 초과했다. event={responseEventId}");
                }

                return response;
            }
            finally
            {
                _requestTracker.CancelTask(sequence);
            }
        }

        protected NetworkDispatchResult Dispatch(ulong sequence, TEventId eventId, TPayload payload)
        {
            ThrowIfDisposed();
            if (false == IsEventIdValid(eventId))
            {
                return NetworkDispatchResult.InvalidEvent;
            }

            if (0 != sequence)
            {
                bool isCompleted = _requestTracker.TryCompleteTask(sequence, eventId, payload, out bool hasWaiter, out _);
                if (true == isCompleted)
                {
                    return NetworkDispatchResult.ResponseCompleted;
                }

                if (true == hasWaiter)
                {
                    if (true == RoutesMismatchedResponse)
                    {
                        _router.Route(eventId, payload);
                    }

                    return NetworkDispatchResult.ResponseEventMismatch;
                }
            }

            _router.Route(eventId, payload);
            return NetworkDispatchResult.Routed;
        }

        protected override void SafeDispose()
        {
            try
            {
                _lifetimeSource.Cancel();
            }
            finally
            {
                _requestTracker.Dispose();
                _router.Dispose();
                _lifetimeSource.Dispose();
            }
        }

        protected void ThrowIfDisposed()
        {
            if (true == IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        protected void ThrowIfInvalidEventId(TEventId eventId)
        {
            if (false == IsEventIdValid(eventId))
            {
                throw new ArgumentOutOfRangeException(nameof(eventId));
            }
        }
    }
}
