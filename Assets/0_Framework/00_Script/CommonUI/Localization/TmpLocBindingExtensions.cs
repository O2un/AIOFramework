using O2un.Core;
using O2un.Core.Localization;
using R3;
using TMPro;

namespace O2un.UI
{
    /// <summary>
    /// <see cref="LocKey"/> 를 TMP 글자에 묶는다.
    ///
    /// UI Toolkit 은 <c>LocalizedString</c> 이 바인딩 수명을 직접 갖지만 TMP 에는 그 경로가 없어
    /// <see cref="RuntimeLocString"/> 이 언어 변경 추종을 대신하고 수명은 <paramref name="owner"/> 가 진다.
    /// </summary>
    public static class TmpLocBindingExtensions
    {
        /// <summary><c>Init</c> 에서 부른다. <c>SafeEnable</c> 에서 부르면 표시 상태가 바뀔 때마다 다시 묶인다.</summary>
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
    }
}
