using O2un.MVVM;
using UnityEngine;

namespace O2un.DEV
{
    [RequireComponent(typeof(LocalizationDemoUguiView))]
    public sealed partial class LocalizationDemoUguiContext : ContextBase<LocalizationDemoUguiView, LocalizationDemoVM>
    {
        protected override LocalizationDemoVM CreateModel()
        {
            return new();
        }
    }
}
