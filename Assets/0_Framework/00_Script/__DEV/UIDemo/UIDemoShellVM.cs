using O2un.Core.Localization;
using O2un.MVVM;
using R3;

namespace O2un.DEV
{
    public sealed class UIDemoShellVM : ViewModelBase
    {
        public static readonly LocKey TITLE = new(LocalTable.UI_Demo, "Shell_Title");
        public static readonly LocKey LOCALIZATION = new(LocalTable.UI_Demo, "Shell_Localization");

        private readonly UIDemoRouter _router;

        public ReadOnlyReactiveProperty<UIDemoPage> CurrentPage => _router.Current;

        public UIDemoShellVM(UIDemoRouter router)
        {
            _router = router;
        }

        public void Select(UIDemoPage page)
        {
            _router.Select(page);
        }
    }
}
