using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace O2un.Core.Localization
{
    /// <summary>
    /// 런타임에 키를 정해 찾는 로컬 문자열.
    ///
    /// <c>SmartLocString</c> 은 인스펙터에 박힌 키의 인자만 갈아 끼우는 물건이라 축이 다르다.
    /// 이쪽은 키 자체가 코드에서 정해지는 자리를 맡는다 — 모드 이름, 클럽 이름, 상태 문구처럼
    /// 무엇을 보여줄지가 실행 중에 결정되는 것들.
    ///
    /// 언어 변경 구독을 자기 안에서 처리한다. 호출부는 <see cref="Text"/> 를 한 번 구독하면 끝이고
    /// <c>SelectedLocaleChanged</c> 를 직접 들 일이 없다.
    /// </summary>
    public sealed class RuntimeLocString : SafeDisposableClass
    {
        // 정적 이벤트라 도메인 리로드 경고가 뜨지만, 델리게이트만 물고 있어 남는 상태가 없다.
        #pragma warning disable UDR0004
        private static readonly EventBinding<Action<Locale>> ON_LOCALE_CHANGED =
            EventBinding.Static<Action<Locale>>(
                h => LocalizationSettings.SelectedLocaleChanged += h,
                h => LocalizationSettings.SelectedLocaleChanged -= h);
        #pragma warning restore UDR0004

        private readonly ReactiveProperty<string> _text = new(string.Empty);

        private LocKey _key;
        private object[] _args;

        // 늦게 도착한 조회가 최신 값을 덮지 않게 하는 표식. 언어를 연달아 바꾸면 순서가 뒤집힌다.
        private int _version;

        public ReadOnlyReactiveProperty<string> Text => _text;

        /// <summary>지금 값. 다른 문구와 이어 붙일 때만 쓴다 — 화면에 꽂을 때는 <see cref="Text"/> 를 구독한다.</summary>
        public string Current => _text.CurrentValue;

        public LocKey Key => _key;

        public RuntimeLocString()
        {
            ON_LOCALE_CHANGED.AddTo(this, OnSelectedLocaleChanged);
        }

        public RuntimeLocString(LocKey key, params object[] args) : this()
        {
            Set(key, args);
        }

        /// <summary>키만 바꾼다. 같은 키면 다시 찾지 않는다.</summary>
        public void SetKey(LocKey key)
        {
            if (_key == key)
            {
                return;
            }

            _key = key;
            Refresh();
        }

        /// <summary>SmartString 인자만 바꾼다. 인자는 값이 같은지 볼 수 없어 항상 다시 찾는다.</summary>
        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        public void Set(LocKey key, params object[] args)
        {
            _key = key;
            _args = args;
            Refresh();
        }

        /// <summary>값이 도착하는 걸 기다리지 않는다. 받는 즉시 <see cref="Text"/> 로 흘러간다.</summary>
        public void Refresh()
        {
            RefreshAsync().Forget();
        }

        /// <summary>다른 문구와 이어 붙여야 해서 값이 지금 있어야 할 때 쓴다.</summary>
        public async UniTask<string> RefreshAsync()
        {
            int version = ++_version;

            string text = (null == _args || 0 == _args.Length)
                ? await _key.GetAsync()
                : await _key.GetAsync(_args);

            // 이 조회가 도는 사이 키가 바뀌었거나 주인이 사라졌다.
            if (version != _version || true == IsDisposed)
            {
                return text;
            }

            _text.Value = text;

            return text;
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            Refresh();
        }

        protected override void SafeDispose()
        {
            _text.Dispose();

            base.SafeDispose();
        }
    }
}
