using O2un.Core;

namespace O2un.MVVM
{
    public abstract class SubViewBase<T> : SafeUI
    {
        public abstract void Bind(T data);

        public virtual void UnBind()
        {
        }
    }
}
