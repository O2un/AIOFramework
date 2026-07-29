using System;
using System.Collections.Generic;
using System.Text.Json;
using Cysharp.Threading.Tasks;

namespace O2un.Core.Network
{
    /// <summary>
    /// 요청별 응답 대기자. 응답 타입을 아는 자리(구독)와 JsonElement 만 아는 자리(수신)가 갈리므로
    /// 타입을 지운 인터페이스로 잇고 역직렬화를 대기자 안에서 수행한다.
    /// </summary>
    public sealed class NetworkRequestTracker
    {
        private interface IRequestWaiter
        {
            string ExpectedEventName { get; }
            void TrySetResult(JsonElement dataElement);
            void TrySetCanceled();
        }

        private sealed class RequestWaiter<T> : IRequestWaiter
        {
            private readonly UniTaskCompletionSource<T> _source = new();
            public string ExpectedEventName { get; }
            public UniTask<T> Task => _source.Task;

            public RequestWaiter(string expectedEventName)
            {
                ExpectedEventName = expectedEventName;
            }

            public void TrySetResult(JsonElement dataElement)
            {
                try
                {
                    _source.TrySetResult(dataElement.CommonOptionDeserialize<T>());
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
        }

        private readonly Dictionary<ulong, IRequestWaiter> _waiters = new();

        public UniTask<T> CreateWaitTask<T>(ulong uniqueKey, string expectedEventName)
        {
            var waiter = new RequestWaiter<T>(expectedEventName);
            _waiters.Add(uniqueKey, waiter);
            return waiter.Task;
        }

        public bool TryCompleteTask(ulong uniqueKey, string eventName, JsonElement dataElement, out bool hasWaiter)
        {
            if (false == _waiters.TryGetValue(uniqueKey, out var waiter))
            {
                hasWaiter = false;
                return false;
            }

            hasWaiter = true;
            if (false == string.Equals(waiter.ExpectedEventName, eventName, StringComparison.Ordinal))
            {
                return false;
            }

            waiter.TrySetResult(dataElement);
            return true;
        }

        public void RemoveTask(ulong uniqueKey)
        {
            _waiters.Remove(uniqueKey);
        }

        public void Clear()
        {
            // 취소하지 않고 비우면 대기 중인 요청이 타임아웃까지 매달린다.
            foreach (var waiter in _waiters.Values)
            {
                waiter.TrySetCanceled();
            }

            _waiters.Clear();
        }
    }
}
