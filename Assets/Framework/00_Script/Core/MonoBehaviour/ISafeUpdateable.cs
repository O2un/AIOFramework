using System;
using System.ComponentModel;

namespace O2un
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("ISafeUpdateable은 내부 시스템용 마커 인터페이스입니다. 직접 참조하거나 사용하지 마세요.")]
    public interface ISafeUpdateable {}
    
#pragma warning disable 618
    public interface ISafeUpdate : ISafeUpdateable
    {
        void SafeUpdate();
    }

    public interface ISafeFixedUpdate : ISafeUpdateable
    {
        void SafeFixedUpdate();
    }

    public interface ISafeLateUpdate : ISafeUpdateable
    {
        void SafeLateUpdate();
    }
#pragma warning restore 618
}
