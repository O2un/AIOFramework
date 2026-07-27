using System;
using System.Collections.Generic;
using O2un.Core;

namespace O2un.Reactive
{
    /// <summary>
    /// 키별 Runtime을 보관하는 레지스트리. 읽기(TReader)와 쓰기(TRuntime)를 같은 인스턴스의
    /// 서로 다른 얼굴로 나눠, 생산자와 소비자가 서로를 직접 참조하지 않게 한다.
    /// </summary>
    public interface ISourceProvider<TKey, TReader, TRuntime>
    {
        TReader GetReader(TKey key);
        TRuntime GetRuntime(TKey key);
    }

    public abstract class SourceProvider<TKey, TReader, TRuntime> : SafeDisposableClass, ISourceProvider<TKey, TReader, TRuntime>
        where TKey : Enum
        where TRuntime : SafeDisposableClass, TReader
    {
        protected readonly Dictionary<TKey, TRuntime> _runtimes = new();

        protected SourceProvider()
        {
            RegistRuntimes();
        }

        protected abstract void RegistRuntimes();

        public TReader GetReader(TKey key)
        {
            return GetRuntime(key);
        }

        public TRuntime GetRuntime(TKey key)
        {
            if (_runtimes.TryGetValue(key, out var runtime))
            {
                return runtime;
            }

            throw new InvalidOperationException($"[{GetType().Name}] Runtime not found. Key: {key}");
        }

        protected override void SafeDispose()
        {
            foreach (var runtime in _runtimes.Values)
            {
                runtime.Dispose();
            }

            _runtimes.Clear();
        }
    }
}
