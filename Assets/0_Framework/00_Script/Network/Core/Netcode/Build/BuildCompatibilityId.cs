using System;
using System.Globalization;

namespace O2un.Core.Network
{
    /// <summary>
    /// 같은 배포 묶음인지 판정하는 단일 정확 일치 키. 값 생성은 빌드 파이프라인이 하고 런타임은 검증만 한다.
    /// </summary>
    public static class BuildCompatibilityId
    {
        public const string TIME_FORMAT = "yyyyMMddHHmmss";
        public const int TIME_LENGTH = 14;
        public const int SUFFIX_LENGTH = 8;
        public const int LENGTH = TIME_LENGTH + 1 + SUFFIX_LENGTH;

        // 저장소 기본값. IsValid 를 통과하지 못해야 생성 단계를 건너뛴 빌드가 조용히 배포되지 않는다.
        public const string NOT_GENERATED = "NOT_GENERATED";

        public static string Current => BuildCompatibilityIdSource.VALUE;

        public static bool IsValid(string id)
        {
            if (true == string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (LENGTH != id.Length)
            {
                return false;
            }

            if ('-' != id[TIME_LENGTH])
            {
                return false;
            }

            if (false == DateTime.TryParseExact(
                    id.Substring(0, TIME_LENGTH),
                    TIME_FORMAT,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out _))
            {
                return false;
            }

            for (var i = TIME_LENGTH + 1; i < LENGTH; ++i)
            {
                char letter = id[i];
                bool isHex = ('0' <= letter && letter <= '9') || ('a' <= letter && letter <= 'f');

                if (false == isHex)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsMatch(string left, string right)
        {
            if (false == IsValid(left) || false == IsValid(right))
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
