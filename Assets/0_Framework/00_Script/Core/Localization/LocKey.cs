using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using O2un.Utils;
using UnityEngine.Localization;

namespace O2un.Core.Localization
{
    /// <summary>
    /// 로컬 문자열 한 칸 — 테이블과 키를 한 쌍으로 묶는다.
    ///
    /// 키만 들고 다니면 호출부마다 테이블을 다시 적게 되고, 그 테이블이 생문자열이거나
    /// <c>enum.ToString()</c> 이라 오타와 할당이 새어 나온다. 테이블은 <see cref="LocalTable"/> 로만 받는다.
    /// </summary>
    public readonly struct LocKey : IEquatable<LocKey>
    {
        public string Table { get; }
        public string Key { get; }

        /// <summary>default(LocKey). 보여줄 문구가 없다는 뜻이다 — 빈 문자열과 같게 다룬다.</summary>
        public bool IsEmpty => true == string.IsNullOrEmpty(Key);

        public LocKey(LocalTable table, string key)
        {
            Table = table.ToTableName();
            Key = key;
        }

        private LocKey(string table, string key)
        {
            Table = table;
            Key = key;
        }

        /// <summary>
        /// 게임 어셈블리의 생성된 테이블 enum 이 들어오는 통로.
        ///
        /// <see cref="LocalTable"/> 은 프레임워크 테이블만 담으므로 게임 테이블은 이 타입으로 받을 수 없고,
        /// 프레임워크가 게임 어셈블리를 참조할 수도 없다. 호출부가 생문자열을 적지 않게 하려는 것이 목적이므로
        /// 생성된 <c>Key(this GameLocalTable, string)</c> 확장 말고 직접 부르지 않는다.
        /// </summary>
        public static LocKey FromTableName(string table, string key)
        {
            return new LocKey(table, key);
        }

        /// <summary>
        /// 지금 언어로 한 번 조회한다. 언어가 바뀌어도 이 값은 그대로다 —
        /// 계속 따라가야 하는 자리에는 <see cref="RuntimeLocString"/> 을 쓴다.
        /// </summary>
        public UniTask<string> GetAsync()
        {
            if (true == IsEmpty)
            {
                return UniTask.FromResult(string.Empty);
            }

            return StringUtils.GetLocalizedString(Table, Key);
        }

        public UniTask<string> GetAsync(params object[] args)
        {
            if (true == IsEmpty)
            {
                return UniTask.FromResult(string.Empty);
            }

            return StringUtils.GetLocalizedString(Table, Key, args);
        }

        public UniTask<string> GetAsync(IList<object> args)
        {
            if (true == IsEmpty)
            {
                return UniTask.FromResult(string.Empty);
            }

            return StringUtils.GetLocalizedString(Table, Key, args);
        }

        /// <summary>
        /// 인스펙터 경로(<c>SmartLocString</c>, <c>LocalizeStringEvent</c>)에 넘길 때 쓴다.
        /// 그쪽은 자기가 언어 변경을 따라가므로 키만 갈아 끼우면 된다.
        /// </summary>
        public LocalizedString ToLocalizedString()
        {
            return new LocalizedString(Table, Key);
        }

        // ReactiveProperty 가 값이 바뀌었는지 볼 때 쓴다. 없으면 ValueType.Equals 의 리플렉션 경로로 샌다.
        public bool Equals(LocKey other)
        {
            return Table == other.Table && Key == other.Key;
        }

        public override bool Equals(object obj)
        {
            return obj is LocKey other && true == Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Table, Key);
        }

        public static bool operator ==(LocKey left, LocKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LocKey left, LocKey right)
        {
            return false == left.Equals(right);
        }

        /// <summary>로그에 찍히는 모양. 번역 누락을 쫓을 때 테이블까지 보여야 한다.</summary>
        public override string ToString()
        {
            return $"{Table}/{Key}";
        }
    }
}
