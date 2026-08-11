using TMPro;

namespace O2un.UI
{
    public static class TmpTextExtensions
    {
        // 숫자 하나를 꽂을 때 호출부가 매번 보간 문자열을 만들지 않게 하는 래퍼다.
        public static void SetText(this TMP_Text tmp, int num)
        {
            tmp.SetText("{0}", num);
        }

        public static void SetText(this TMP_Text tmp, float num)
        {
            tmp.SetText("{0:0.0}", num);
        }
    }
}
