using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Localization;
using O2un.MVVM;
using O2un.UI;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace O2un.DEV
{
    /// <summary>
    /// 데모의 uGUI(TMP) 쪽. 같은 문구를 세 가지 경로로 띄워 축이 다르다는 것을 보여준다.
    ///
    /// - <c>BindLoc(LocKey)</c> — 인자 없는 고정 문구
    /// - <see cref="SmartLocString"/> — 인스펙터에 키가 박혀 있고 인자만 갈아 끼우는 자리
    /// - <see cref="RuntimeLocString.SetKey"/> — 무엇을 보여줄지가 실행 중에 정해지는 자리
    /// </summary>
    public sealed class LocalizationDemoUguiView : ViewBase<LocalizationDemoVM>
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [SerializeField] private TMP_Text _status;

        [SerializeField] private SmartLocString _greeting;
        [SerializeField] private SmartLocString _itemCount;
        [SerializeField] private SmartLocString _currentLanguage;

        [SerializeField] private Button _languageButton;
        [SerializeField] private TMP_Text _languageButtonLabel;
        [SerializeField] private Button _statusButton;

        private RuntimeLocString _statusLoc;

        protected override async UniTask Init(CancellationToken ct)
        {
            await base.Init(ct);

            _title.BindLoc(LocalizationDemoVM.TITLE, this);
            _subtitle.BindLoc(LocalizationDemoVM.SUBTITLE, this);
            _languageButtonLabel.BindLoc(LocalizationDemoVM.LANGUAGE_LABEL, this);

            _statusLoc = _status.BindLoc(LocalizationDemoVM.STATUS_IDLE, this);

            this.AddListener(_languageButton.onClick, OnLanguageClicked);
            this.AddListener(_statusButton.onClick, OnStatusClicked);
        }

        protected override void BindModel()
        {
            _greeting.UpdateData<LocalizationDemoVM.GreetingArgs>(x => x.PlayerName = Model.PlayerName);
            _itemCount.UpdateData<LocalizationDemoVM.CountArgs>(x => x.Count = Model.ItemCount);

            Model.LanguageName.Subscribe(OnLanguageNameChanged).AddTo(DisposableR3);
            Model.StatusKey.Subscribe(OnStatusKeyChanged).AddTo(DisposableR3);
        }

        private void OnLanguageNameChanged(string languageName)
        {
            _currentLanguage.UpdateData<LocalizationDemoVM.LanguageArgs>(x => x.Language = languageName);
        }

        private void OnStatusKeyChanged(LocKey key)
        {
            _statusLoc.SetKey(key);
        }

        private void OnLanguageClicked()
        {
            Model.CycleLocale();
        }

        private void OnStatusClicked()
        {
            Model.CycleStatus();
        }
    }
}
