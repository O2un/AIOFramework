using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.DEV
{
    [RequireComponent(typeof(UIDemoShellView))]
    public sealed partial class UIDemoShellContext : ContextBase<UIDemoShellView, UIDemoShellVM>
    {
        [Inject] private UIDemoRouter _router;

        protected override UIDemoShellVM CreateModel()
        {
            return new(_router);
        }
    }
}
