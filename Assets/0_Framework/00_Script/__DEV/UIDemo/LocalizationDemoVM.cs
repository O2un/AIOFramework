using System;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Localization;
using O2un.MVVM;
using R3;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace O2un.DEV
{
    /// <summary>
    /// 로컬라이즈 데모 화면의 ViewModel.
    ///
    /// 언어 상태를 들고 있지 않다 — 정본은 <c>LocalizationSettings.SelectedLocale</c> 이고 이 클래스는
    /// 그것을 향한 창이다. 그래서 UI Toolkit 패널과 uGUI 패널이 각자 인스턴스를 가져도 분기하지 않는다.
    /// </summary>
    public sealed class LocalizationDemoVM : ViewModelBase
    {
        // SmartString 이 멤버 이름으로 자리를 찾는다. 테이블의 {PlayerName} 과 철자가 어긋나면 조용히 빈칸이 된다.
        public sealed class GreetingArgs
        {
            public string PlayerName { get; set; }
        }

        public sealed class CountArgs
        {
            public int Count { get; set; }
        }

        public sealed class LanguageArgs
        {
            public string Language { get; set; }
        }

        public const string VAR_PLAYER_NAME = "PlayerName";
        public const string VAR_COUNT = "Count";
        public const string VAR_LANGUAGE = "Language";

        public static readonly LocKey TITLE = new(LocalTable.UI_Demo, "Title");
        public static readonly LocKey SUBTITLE = new(LocalTable.UI_Demo, "Subtitle");
        public static readonly LocKey GREETING = new(LocalTable.UI_Demo, "Greeting");
        public static readonly LocKey ITEM_COUNT = new(LocalTable.UI_Demo, "ItemCount");
        public static readonly LocKey CURRENT_LANGUAGE = new(LocalTable.UI_Demo, "CurrentLanguage");

        public static readonly LocKey LANGUAGE_LABEL = new(LocalTable.UI_Common, "Language");

        public static readonly LocKey STATUS_IDLE = new(LocalTable.UI_Demo, "Status_Idle");
        public static readonly LocKey STATUS_RUNNING = new(LocalTable.UI_Demo, "Status_Running");
        public static readonly LocKey STATUS_DONE = new(LocalTable.UI_Demo, "Status_Done");

        private static readonly LocKey[] STATUS_CYCLE = { STATUS_IDLE, STATUS_RUNNING, STATUS_DONE };

        private const string DEMO_PLAYER_NAME = "O2un";
        private const int DEMO_ITEM_COUNT = 3;

        // 정적 이벤트라 도메인 리로드 경고가 뜨지만, 델리게이트만 물고 있어 남는 상태가 없다.
        #pragma warning disable UDR0004
        private static readonly EventBinding<Action<Locale>> ON_LOCALE_CHANGED =
            EventBinding.Static<Action<Locale>>(
                h => LocalizationSettings.SelectedLocaleChanged += h,
                h => LocalizationSettings.SelectedLocaleChanged -= h);
        #pragma warning restore UDR0004

        private readonly ReactiveProperty<string> _languageName = new(string.Empty);
        private readonly ReactiveProperty<LocKey> _statusKey = new(STATUS_IDLE);

        private int _statusIndex;

        public ReadOnlyReactiveProperty<string> LanguageName => _languageName;
        public ReadOnlyReactiveProperty<LocKey> StatusKey => _statusKey;

        public string PlayerName => DEMO_PLAYER_NAME;
        public int ItemCount => DEMO_ITEM_COUNT;

        public LocalizationDemoVM(UIDemoRouter router)
        {
            ON_LOCALE_CHANGED.AddTo(this, OnSelectedLocaleChanged);

            router.Current.Subscribe(OnPageChanged).AddTo(DisposableR3);
        }

        private void OnPageChanged(UIDemoPage page)
        {
            SetVisible(UIDemoPage.Localization == page);
        }

        public override async UniTask InitAsync()
        {
            // AvailableLocales 는 Addressables 로 채워진다. 초기화 전에 읽으면 목록이 비어 있어 순환이 죽는다.
            await UniTask.WaitUntil(() => true == LocalizationSettings.InitializationOperation.IsDone);

            // 기다리는 사이 씬이 언로드되면 여기까지 깨어난다. 그때 ReactiveProperty 는 이미 Dispose 된 뒤다.
            if (true == IsDisposed)
            {
                return;
            }

            _languageName.Value = ToLocaleName(LocalizationSettings.SelectedLocale);
        }

        public void CycleLocale()
        {
            var locales = LocalizationSettings.AvailableLocales?.Locales;
            if (null == locales || 0 == locales.Count)
            {
                return;
            }

            int index = locales.IndexOf(LocalizationSettings.SelectedLocale);

            LocalizationSettings.SelectedLocale = locales[(index + 1) % locales.Count];
        }

        public void CycleStatus()
        {
            _statusIndex = (_statusIndex + 1) % STATUS_CYCLE.Length;
            _statusKey.Value = STATUS_CYCLE[_statusIndex];
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            _languageName.Value = ToLocaleName(locale);
        }

        private static string ToLocaleName(Locale locale)
        {
            return null != locale ? locale.LocaleName : string.Empty;
        }

        protected override void SafeDispose()
        {
            _languageName.Dispose();
            _statusKey.Dispose();

            base.SafeDispose();
        }
    }
}
