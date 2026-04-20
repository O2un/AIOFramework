using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace O2un.Core.Network
{
    public sealed class NetworkRequestTracker
    {
        private readonly Dictionary<string, object> _waitingTasks = new(StringComparer.Ordinal);
    
        public UniTask<T> CreateWaitTask<T>(string waitEventName)
        {
            var tcs = new UniTaskCompletionSource<T>();
            _waitingTasks[waitEventName] = tcs;
            return tcs.Task;
        }
    
        public bool TryCompleteTask<T>(string eventName, T data)
        {
            if (_waitingTasks.TryGetValue(eventName, out var tcsObj) && tcsObj is UniTaskCompletionSource<T> tcs)
            {
                tcs.TrySetResult(data);
                _waitingTasks.Remove(eventName);
                return true;
            }
            return false;
        }
    
        public void RemoveTask(string waitEventName)
        {
            _waitingTasks.Remove(waitEventName);
        }
    
        public void Clear()
        {
            _waitingTasks.Clear();
        }
    }
}
