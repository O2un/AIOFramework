using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UIElements;

namespace O2un.Core.Localization
{
    /// <summary>
    /// <see cref="LocKey"/> 를 UI Toolkit 글자와 인스펙터 컴포넌트에 꽂는 어댑터.
    /// TMP 쪽은 <c>O2un.UI</c> 의 <c>TmpLocBindingExtensions</c> 가 맡는다 — Core 에 TMP 참조가 없다.
    /// </summary>
    public static class LocBindingExtensions
    {
        private const string TEXT_PROPERTY = "text";

        /// <summary>
        /// UI Toolkit 글자를 로컬 키에 묶는다. 언어 변경과 요소 attach/detach 는 LocalizedString 이
        /// CustomBinding 으로 처리한다. SmartString 인자를 넣으려면 돌려받은 값에 <see cref="SetVar"/> 를 쓴다.
        /// </summary>
        /// <returns>빈 키면 null.</returns>
        public static LocalizedString SetLoc(this TextElement element, LocKey key)
        {
            if (null == element)
            {
                return null;
            }

            if (true == key.IsEmpty)
            {
                element.ClearBinding(TEXT_PROPERTY);
                element.text = string.Empty;
                return null;
            }

            LocalizedString loc = key.ToLocalizedString();
            element.SetBinding(TEXT_PROPERTY, loc);

            return loc;
        }

        public static void SetVar(this LocalizedString loc, string name, int value)
        {
            loc.SetVar<IntVariable, int>(name, value);
        }

        public static void SetVar(this LocalizedString loc, string name, float value)
        {
            loc.SetVar<FloatVariable, float>(name, value);
        }

        public static void SetVar(this LocalizedString loc, string name, string value)
        {
            loc.SetVar<StringVariable, string>(name, value);
        }

        public static void SetVar(this LocalizedString loc, string name, bool value)
        {
            loc.SetVar<BoolVariable, bool>(name, value);
        }

        // 인자를 Arguments 로 넣으면 값이 바뀌어도 바인딩에 알리지 않는다. Variable<T> 는 대입만으로 다시 칠해진다.
        private static void SetVar<TVariable, TValue>(this LocalizedString loc, string name, TValue value)
            where TVariable : Variable<TValue>, new()
        {
            if (null == loc)
            {
                return;
            }

            if (true == loc.TryGetValue(name, out IVariable found) && found is TVariable variable)
            {
                variable.Value = value;
                return;
            }

            loc[name] = new TVariable { Value = value };
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
