using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.DEV
{
    [RequireComponent(typeof(LocalizationDemoUguiView))]
    public sealed partial class LocalizationDemoUguiContext : ContextBase<LocalizationDemoUguiView, LocalizationDemoVM>
    {
        [Inject] private UIDemoRouter _router;

        protected override LocalizationDemoVM CreateModel()
        {
            return new(_router);
        }
    }
}
