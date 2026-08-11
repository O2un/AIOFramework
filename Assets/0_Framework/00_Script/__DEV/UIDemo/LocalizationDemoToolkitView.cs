using O2un.Core;
using O2un.Core.Localization;
using O2un.Core.Utils;
using O2un.MVVM;
using R3;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace O2un.DEV
{
    /// <summary>
    /// 데모의 UI Toolkit 쪽. <c>SetLoc</c> 이 꽂은 <see cref="LocalizedString"/> 바인딩이 언어 변경을
    /// 스스로 따라가므로 글자에 대한 VM 구독이 없다. SmartString 인자만 <c>SetVar</c> 로 갈아 끼운다.
    /// </summary>
    public sealed class LocalizationDemoToolkitView : ViewBaseToolkit<LocalizationDemoVM>
    {
        private LocalizedString _greeting;
        private LocalizedString _itemCount;
        private LocalizedString _currentLanguage;

        protected override void BindElements(VisualElement root)
        {
            // UIDocument 루트는 화면 전체를 덮는다. 그대로 두면 옆에 놓인 uGUI 패널의 클릭을 전부 삼킨다.
            if (null != root.parent)
            {
                root.parent.pickingMode = PickingMode.Ignore;
            }

            root.QRequired<Label>("Title").SetLoc(LocalizationDemoVM.TITLE);
            root.QRequired<Label>("Subtitle").SetLoc(LocalizationDemoVM.SUBTITLE);

            _greeting = root.QRequired<Label>("Greeting").SetLoc(LocalizationDemoVM.GREETING);
            _itemCount = root.QRequired<Label>("ItemCount").SetLoc(LocalizationDemoVM.ITEM_COUNT);
            _currentLanguage = root.QRequired<Label>("CurrentLanguage").SetLoc(LocalizationDemoVM.CURRENT_LANGUAGE);

            root.QRequired<Label>("StatusIdle").SetLoc(LocalizationDemoVM.STATUS_IDLE);
            root.QRequired<Label>("StatusRunning").SetLoc(LocalizationDemoVM.STATUS_RUNNING);
            root.QRequired<Label>("StatusDone").SetLoc(LocalizationDemoVM.STATUS_DONE);

            Button languageButton = root.QRequiredBinding<Button>("LanguageButton")
                                        .Clicked(OnLanguageClicked)
                                        .AddTo(DisposableR3).Element;

            languageButton.SetLoc(LocalizationDemoVM.LANGUAGE_LABEL);
        }

        protected override void BindModel()
        {
            _greeting.SetVar(LocalizationDemoVM.VAR_PLAYER_NAME, Model.PlayerName);
            _itemCount.SetVar(LocalizationDemoVM.VAR_COUNT, Model.ItemCount);

            Model.LanguageName.Subscribe(OnLanguageNameChanged).AddTo(DisposableR3);
        }

        private void OnLanguageNameChanged(string languageName)
        {
            _currentLanguage.SetVar(LocalizationDemoVM.VAR_LANGUAGE, languageName);
        }

        private void OnLanguageClicked()
        {
            Model.CycleLocale();
        }
    }
}
