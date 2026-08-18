using O2un.Core;
using O2un.Core.Localization;
using O2un.Core.Utils;
using O2un.MVVM;
using R3;
using UnityEngine.UIElements;

namespace O2un.DEV
{
    /// <summary>
    /// 데모 허브의 상단 바. 데모 하나를 늘리는 비용이 <see cref="MENU"/> 한 줄과 UXML 한 줄이 되도록
    /// 페이지·요소 이름·라벨을 표로 두고 배선은 순회로 처리한다.
    /// </summary>
    public sealed class UIDemoShellView : ViewBaseToolkit<UIDemoShellVM>
    {
        private readonly struct MenuEntry
        {
            public readonly UIDemoPage Page;
            public readonly string ElementName;
            public readonly LocKey Label;

            public MenuEntry(UIDemoPage page, string elementName, LocKey label)
            {
                Page = page;
                ElementName = elementName;
                Label = label;
            }
        }

        private static readonly MenuEntry[] MENU =
        {
            new(UIDemoPage.Localization, "LocalizationButton", UIDemoShellVM.LOCALIZATION),
        };

        private const string SELECTED_CLASS = "accent";

        private readonly Button[] _menuButtons = new Button[MENU.Length];

        protected override void BindElements(VisualElement root)
        {
            // UIDocument 루트는 화면 전체를 덮는다. 이 바는 데모 패널보다 위에 깔리므로,
            // 그대로 두면 아래에 있는 두 패널의 클릭을 전부 삼킨다.
            if (null != root.parent)
            {
                root.parent.pickingMode = PickingMode.Ignore;
            }

            root.QRequired<Label>("ShellTitle").SetLoc(UIDemoShellVM.TITLE);

            for (int i = 0; i < MENU.Length; ++i)
            {
                MenuEntry entry = MENU[i];

                Button button = root.QRequiredBinding<Button>(entry.ElementName)
                                    .Clicked(() => Model.Select(entry.Page))
                                    .AddTo(DisposableR3).Element;

                button.SetLoc(entry.Label);

                _menuButtons[i] = button;
            }
        }

        protected override void BindModel()
        {
            Model.CurrentPage.Subscribe(OnPageChanged).AddTo(DisposableR3);
        }

        private void OnPageChanged(UIDemoPage page)
        {
            for (int i = 0; i < MENU.Length; ++i)
            {
                _menuButtons[i].EnableInClassList(SELECTED_CLASS, MENU[i].Page == page);
            }
        }
    }
}
