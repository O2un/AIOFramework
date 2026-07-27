using System;
using System.Collections.Generic;
using R3;

namespace O2un.Core.Events
{
    public interface IEventPublisher<TCategory>
    {
        void Publish<TEvent>(TEvent message) where TEvent : TCategory;
    }

    public interface IEventSubscriber<TCategory>
    {
        Observable<TEvent> Observe<TEvent>() where TEvent : TCategory;
    }

    public class EventBus<TCategory> : SafeDisposableClass, IEventPublisher<TCategory>, IEventSubscriber<TCategory>
    {
        private readonly Dictionary<Type, object> _subjectMap = new();

        public void Publish<TEvent>(TEvent message) where TEvent : TCategory
        {
            GetSubject<TEvent>().OnNext(message);
        }

        public Observable<TEvent> Observe<TEvent>() where TEvent : TCategory
        {
            return GetSubject<TEvent>();
        }

        protected override void SafeDispose()
        {
            foreach (var subject in _subjectMap.Values)
            {
                if (subject is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _subjectMap.Clear();

            base.SafeDispose();
        }

        private Subject<TEvent> GetSubject<TEvent>() where TEvent : TCategory
        {
            Type type = typeof(TEvent);

            if (_subjectMap.TryGetValue(type, out object subject))
            {
                return (Subject<TEvent>)subject;
            }

            var created = new Subject<TEvent>();
            _subjectMap.Add(type, created);

            return created;
        }
    }
}
