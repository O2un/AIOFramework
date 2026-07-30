using System;
using System.Collections.Generic;
using R3;

namespace O2un.Core.Network
{
    public sealed class NetworkRouter<TEventId, TPayload> : SafeDisposableClass
    {
        private readonly Dictionary<TEventId, Subject<TPayload>> _eventSubjects;

        public NetworkRouter(IEqualityComparer<TEventId> comparer)
        {
            _eventSubjects = new Dictionary<TEventId, Subject<TPayload>>(comparer);
        }

        public Observable<TPayload> Observe(TEventId eventId)
        {
            ThrowIfDisposed();
            return GetSubject(eventId);
        }

        public void Route(TEventId eventId, TPayload payload)
        {
            ThrowIfDisposed();
            if (false == _eventSubjects.TryGetValue(eventId, out var subject))
            {
                return;
            }

            subject.OnNext(payload);
        }

        protected override void SafeDispose()
        {
            foreach (var subject in _eventSubjects.Values)
            {
                subject.Dispose();
            }

            _eventSubjects.Clear();
        }

        private Subject<TPayload> GetSubject(TEventId eventId)
        {
            if (true == _eventSubjects.TryGetValue(eventId, out var subject))
            {
                return subject;
            }

            var created = new Subject<TPayload>();
            _eventSubjects.Add(eventId, created);
            return created;
        }

        private void ThrowIfDisposed()
        {
            if (true == IsDisposed)
            {
                throw new ObjectDisposedException(nameof(NetworkRouter<TEventId, TPayload>));
            }
        }
    }
}
