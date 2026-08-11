using VContainer;

namespace O2un.DI
{
    /// <summary>
    /// 데모 씬 스코프. 등록할 서비스가 없고, 부모의 <c>_uiRoots</c> 순회로 Context 를
    /// <c>Initialize()</c> 시키는 것이 유일한 역할이다 — <c>SafeMono</c> 는 스스로 시작하지 않는다.
    /// </summary>
    public sealed class UIDemoSceneScope : CommonLifetimeScope
    {
        protected override void ConfigureScene(IContainerBuilder builder)
        {
            // NULL
        }
    }
}
