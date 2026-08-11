using O2un.MVVM;
using UnityEngine;

namespace O2un.DEV
{
    [RequireComponent(typeof(LocalizationDemoToolkitView))]
    public sealed partial class LocalizationDemoToolkitContext : ContextBase<LocalizationDemoToolkitView, LocalizationDemoVM>
    {
        protected override LocalizationDemoVM CreateModel()
        {
            return new();
        }
    }
}
