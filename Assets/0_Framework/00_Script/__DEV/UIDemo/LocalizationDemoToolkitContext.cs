using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.DEV
{
    [RequireComponent(typeof(LocalizationDemoToolkitView))]
    public sealed partial class LocalizationDemoToolkitContext : ContextBase<LocalizationDemoToolkitView, LocalizationDemoVM>
    {
        [Inject] private UIDemoRouter _router;

        protected override LocalizationDemoVM CreateModel()
        {
            return new(_router);
        }
    }
}
