using O2un.DI;
using VContainer;

namespace O2un.DEV
{
    /// <summary>
    /// 데모 씬 스코프. 등록하는 것은 허브와 데모 패널들이 공유할 <see cref="UIDemoRouter"/> 하나뿐이고,
    /// 나머지는 부모의 <c>_uiRoots</c> 순회로 Context 를 <c>Initialize()</c> 시키는 역할이다
    /// — <c>SafeMono</c> 는 스스로 시작하지 않는다.
    /// </summary>
    public sealed class UIDemoSceneScope : CommonLifetimeScope
    {
        protected override void ConfigureScene(IContainerBuilder builder)
        {
            builder.Register<UIDemoRouter>(Lifetime.Scoped);
        }
    }
}
