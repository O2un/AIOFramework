using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Roslyn.Generator;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace O2un.UI
{
    /// <summary>
    /// 인스펙터에 박힌 키의 SmartString 인자만 갈아 끼우는 TMP 라벨.
    ///
    /// 키 자체가 코드에서 정해지는 자리는 <c>RuntimeLocString</c> 이 맡는다 — 축이 다르다.
    /// 인자를 한 번도 넣지 않으면 아무것도 표시하지 않는다. 인자 없는 문구는 <c>LocalizeStringEvent</c> 를 쓴다.
    /// </summary>
    public partial class SmartLocString : SafeMono
    {
        [RequireComponentField] private TextMeshProUGUI _locText;

        [SerializeField] private LocalizedString _loc;

        private readonly object[] _args = new object[1];
        private bool _isSubscribed;

        protected override async UniTask Init(CancellationToken ct)
        {
            await base.Init(ct);

            _loc.Arguments = _args;
        }

        protected override void SafeDestroy()
        {
            UnsubscribeLocEvent();

            base.SafeDestroy();
        }

        public void UpdateData<T>(Action<T> updateAction) where T : class, new()
        {
            InitData<T>();

            updateAction?.Invoke(_args[0] as T);

            RefreshString();
        }

        private void InitData<T>() where T : class, new()
        {
            if (null == _args[0] || false == (_args[0] is T))
            {
                _args[0] = new T();
            }
        }

        private void RefreshString()
        {
            if (null == _loc)
            {
                return;
            }

            // 파괴된 뒤에도 데이터를 밀어 넣는 호출부가 있다. Unity 의 == 오버로드가 이걸 잡는다.
            if (null == this)
            {
                return;
            }

            // 첫 구독이 곧 첫 조회다. 인자가 채워지기 전에 구독하면 빈 문구가 한 번 지나간다.
            if (false == _isSubscribed)
            {
                SubscribeLocEvent();
                return;
            }

            _loc.RefreshString();
        }

        private void SubscribeLocEvent()
        {
            if (true == _isSubscribed || null == _loc)
            {
                return;
            }

            _loc.Arguments = _args;
            _loc.StringChanged += OnStringChanged;
            _isSubscribed = true;
        }

        private void UnsubscribeLocEvent()
        {
            if (false == _isSubscribed || null == _loc)
            {
                return;
            }

            _loc.StringChanged -= OnStringChanged;
            _isSubscribed = false;
        }

        private void OnStringChanged(string translatedText)
        {
            LocText.SetText(translatedText);
        }
    }
}
