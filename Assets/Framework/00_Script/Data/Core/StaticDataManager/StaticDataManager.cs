using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Utils;
using O2un.Utils;

namespace O2un.Data 
{
    public interface IStaticDataManager
    {
        bool IsLoaded {get;}
        void Load(bool isLoadFromBinary = false, bool isLoadFromAddressables = false);
        void Set();
        void Link();
        UniTask WaitForLoadedAsync(CancellationToken cancellationToken = default);
    }

    public abstract partial class StaticDataManager<T> : IStaticDataManager where T : StaticData, new()
    {
        protected ImmutableDictionary<UniqueKey, T> DataList { get; private protected set; } = ImmutableDictionary<UniqueKey, T>.Empty;

        private UniTaskCompletionSource _initCompletionSource;
        public bool IsLoaded {get; private set;}
        public UniTask WaitForLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoaded) return UniTask.CompletedTask;
            return _initCompletionSource.Task.AttachExternalCancellation(cancellationToken);
        }


        public T Get(UniqueKey key)
        {
            return DataList.GetValueOrDefault(key.Raw);
        }

        protected virtual void Clear()
        {
            IsLoaded = false;
            DataList = ImmutableDictionary<UniqueKey, T>.Empty;
            if (_initCompletionSource.Task.Status.IsCompleted())
            {
                _initCompletionSource = new UniTaskCompletionSource();
            }
        }

        protected void CompleteLoad()
        {
            IsLoaded = true;
            _initCompletionSource.TrySetResult();
        }

        public void Load(bool isLoadFromBinary = false, bool isLoadFromAddressable = false)
        {
        }
        
        public void Set()
        {
            foreach(var data in DataList.Values)
            {
                if(false == data.Set())
                {
                    Log.Print(Log.LogLevel.Error, $"데이터 Set 실패 {data.Key}");
                    CommonUtils.Quit();
                }
            }
            SetProcess();
        }
        protected abstract void SetProcess();

        public void Link()
        {
            foreach(var data in DataList.Values)
            {
                if(false == data.Link())
                {
                    Log.Print(Log.LogLevel.Error, $"데이터 Link 실패 {data.Key}");
                    CommonUtils.Quit();
                }
            }
            LinkProcess();
        }
        protected abstract void LinkProcess();
    }
}