using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;
using R3;

namespace O2un.Core
{
    // SafeClass를 상속받지 못하는데 Dispose 해야하는 클래스에 붙이기
    public interface ISafeDisposable : IDisposable
    {
        bool IsDisposed {get;}
        CompositeDisposable DisposableR3 {get;}
        Dictionary<string, SerialDisposable> ExclusiveTasks { get; }
    }

    public abstract class SafeDisposableClass : ISafeDisposable
    {
        #region ASYNC_HELPER
        protected readonly CompositeDisposable _disposableR3 = new();
        public CompositeDisposable DisposableR3 => _disposableR3;

        private readonly Dictionary<string, SerialDisposable> _exclusiveTasks = new();
        public Dictionary<string, SerialDisposable> ExclusiveTasks => _exclusiveTasks;
        private bool _disposed = false;
        public bool IsDisposed => _disposed;
        #endregion


        public void Dispose()
        {
            if(IsDisposed) return;
            _disposed = true;
            _disposableR3.Dispose();
            SafeDispose();
        }

        protected abstract void SafeDispose();
    }
}
