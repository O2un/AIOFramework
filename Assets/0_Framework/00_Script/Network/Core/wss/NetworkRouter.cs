using System;
using System.Collections.Generic;
using System.Text.Json;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 서버 푸시 라우터. 응답 타입은 구독 시점에만 알 수 있으므로 핸들러를 JsonElement 래퍼로 감싸 보관한다.
    /// </summary>
    public sealed class NetworkRouter : SafeDisposableClass
    {
        private readonly Dictionary<string, Subject<JsonElement>> _eventSubjects = new(StringComparer.Ordinal);

        public Observable<T> Observe<T>(string eventName)
        {
            return GetSubject(eventName).Select(payload => payload.CommonOptionDeserialize<T>());
        }

        public Observable<T> Observe<T>(string eventName, Func<JsonElement, T> parser)
        {
            return GetSubject(eventName).Select(parser);
        }

        public void Route(string eventName, JsonElement payload)
        {
            if (false == _eventSubjects.TryGetValue(eventName, out var subject))
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

        private Subject<JsonElement> GetSubject(string eventName)
        {
            if (_eventSubjects.TryGetValue(eventName, out var subject))
            {
                return subject;
            }

            var created = new Subject<JsonElement>();
            _eventSubjects.Add(eventName, created);
            return created;
        }
    }
}
