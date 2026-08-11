using O2un.Core;
using O2un.Core.Localization;
using R3;
using TMPro;
using UnityEngine.Localization.Components;

namespace O2un.UI
{
    /// <summary>
    /// <see cref="LocKey"/> 를 TMP 글자와 인스펙터 컴포넌트에 꽂는 어댑터.
    /// UI Toolkit 쪽은 <c>O2un.Core</c> 의 <c>VisualElementLocExtensions</c> 가 맡는다 — TMP 참조가 거기엔 없다.
    /// </summary>
    public static class LocBindingExtensions
    {
        /// <summary>
        /// TMP 글자를 로컬 문자열에 묶는다. 수명은 <paramref name="owner"/> 를 따른다.
        ///
        /// <c>Init</c> 에서 부른다. <c>SafeEnable</c> 에서 부르면 표시 상태가 바뀔 때마다 다시 묶인다.
        /// </summary>
        public static RuntimeLocString BindLoc(this TMP_Text label, LocKey key, ISafeDisposable owner)
        {
            RuntimeLocString loc = new(key);

            return label.BindLoc(loc, owner);
        }

        public static RuntimeLocString BindLoc(this TMP_Text label, RuntimeLocString loc, ISafeDisposable owner)
        {
            loc.AddTo(owner.DisposableR3);
            loc.Text.Subscribe(x => label.SetText(x)).AddTo(owner.DisposableR3);

            return loc;
        }

        /// <summary>
        /// 인스펙터에 붙은 <see cref="LocalizeStringEvent"/> 의 키를 갈아 끼운다.
        /// 갱신은 그쪽이 알아서 하므로 테이블·키만 넘긴다.
        /// </summary>
        public static void SetLoc(this LocalizeStringEvent target, LocKey key)
        {
            if (null == target)
            {
                return;
            }

            target.SetTable(key.Table);
            target.SetEntry(key.Key);
        }
    }
}
