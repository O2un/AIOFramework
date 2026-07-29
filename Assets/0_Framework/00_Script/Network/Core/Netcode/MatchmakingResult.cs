using System;
using System.Collections.Generic;

namespace O2un.Core.Network
{
    /// <summary>
    /// 거절 사유를 UI 까지 올리기 위한 봉투. 응답을 못 받은 것과 서버가 거절한 것을 화면이 구분할 수 있어야 하므로
    /// 접속 정보만 돌려주지 않고 사유를 함께 싣는다.
    /// </summary>
    public sealed class MatchmakingResult
    {
        public bool IsSuccess { get; }
        public string Reason { get; }

        private MatchmakingResult(bool isSuccess, string reason)
        {
            IsSuccess = isSuccess;
            Reason = reason;
        }

        public static MatchmakingResult Success() => new(true, null);
        public static MatchmakingResult Failure(string reason) => new(false, reason);
    }

    public sealed class MatchmakingResult<T> where T : class
    {
        public bool IsSuccess { get; }
        public string Reason { get; }
        public T Value { get; }

        private MatchmakingResult(bool isSuccess, string reason, T value)
        {
            IsSuccess = isSuccess;
            Reason = reason;
            Value = value;
        }

        public static MatchmakingResult<T> Success(T value) => new(true, null, value);
        public static MatchmakingResult<T> Failure(string reason) => new(false, reason, null);
    }

    /// <summary>
    /// 화면에 올려도 되는 사유만 통과시키는 필터. 서버가 JavaScript 라 컴파일러가 표기 불일치를 잡아주지 못하므로
    /// 클라이언트가 아는 사유를 여기 한 곳에만 적는다.
    /// </summary>
    public static class MatchmakingReasons
    {
        // 사용자가 행동을 바꿔 해결할 수 있는 것만 공개한다.
        public const string ROOM_NOT_FOUND = "ROOM_NOT_FOUND";
        public const string ROOM_FULL = "ROOM_FULL";
        public const string NOT_IN_ROOM = "NOT_IN_ROOM";
        public const string NO_RESPONSE = "NO_RESPONSE";

        // 공개 목록에 없는 사유는 전부 이것으로 접힌다.
        public const string REQUEST_REJECTED = "REQUEST_REJECTED";

        // 서버 푸시(SessionClosedNotice) 사유라 Ack 와 경로가 다르고 이 필터를 타지 않는다.
        public const string HOST_LEFT = "HOST_LEFT";
        public const string CONNECTION_LOST = "CONNECTION_LOST";

        private static readonly HashSet<string> PUBLIC_REASONS = new(StringComparer.Ordinal)
        {
            ROOM_NOT_FOUND,
            ROOM_FULL,
            NOT_IN_ROOM,
            NO_RESPONSE,
        };

        /// <summary>
        /// INVALID_PORT 처럼 사용자가 손댈 수 없는 사유가 그대로 올라가면 화면이 서버 내부 검증 규칙을 알려주는 꼴이 된다.
        /// 원문은 버리지 않고 호출부가 로그에 남긴다.
        /// </summary>
        public static bool IsPublic(string reason)
        {
            if (true == string.IsNullOrEmpty(reason))
            {
                return false;
            }

            return PUBLIC_REASONS.Contains(reason);
        }
    }
}
