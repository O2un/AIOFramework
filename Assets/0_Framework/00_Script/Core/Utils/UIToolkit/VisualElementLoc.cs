using O2un.Core.Localization;
using O2un.Utils;
using R3;
using UnityEngine.UIElements;

namespace O2un.Core.Utils
{
    /// <summary>
    /// <see cref="LocKey"/> 를 UI Toolkit 글자 자리에 꽂는 어댑터.
    ///
    /// 언어 변경 추종은 <see cref="RuntimeLocString"/> 이 하고, 해제는 <see cref="VisualElementBinding{TElement}"/> 가 한다.
    /// 그래서 바인딩 수명은 요소가 아니라 그 바인딩을 들고 있는 <b>뷰</b> 를 따라간다.
    /// </summary>
    public static class VisualElementLocExtensions
    {
        /// <summary>
        /// 키를 꽂고 바인딩을 그대로 돌려준다 — 질의 체인 안에서 쓴다.
        /// 나중에 키나 인자를 갈아 끼워야 하면 <see cref="BindLoc{TElement}"/> 를 쓴다.
        /// </summary>
        public static VisualElementBinding<TElement> SetLoc<TElement>(this VisualElementBinding<TElement> binding, LocKey key, params object[] args) where TElement : TextElement
        {
            binding.BindLoc(key, args);

            return binding;
        }

        /// <summary>
        /// 키를 꽂고 <see cref="RuntimeLocString"/> 을 돌려준다.
        /// 받아오는 동안은 이전 문구가 남는다. 빈 칸이 깜빡이는 것보다 낫다.
        /// </summary>
        public static RuntimeLocString BindLoc<TElement>(this VisualElementBinding<TElement> binding, LocKey key, params object[] args) where TElement : TextElement
        {
            binding.ThrowIfNull();

            TElement element = binding.Element;
            RuntimeLocString loc = new(key, args);

            loc.Text.Subscribe(text => element.text = text).AddTo(loc.DisposableR3);

            binding.Track(
                loc,
                static (_, _) => { /* NULL */ },
                static (_, target) => target.Dispose());

            return loc;
        }
    }
}
