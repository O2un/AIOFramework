using System;
using O2un.Core;
using UnityEngine;

namespace O2un.DI
{
    /// <summary>
    /// 디버그 UI 런타임 스위치. Resources/SystemConfig/DebugConfig.asset 을 읽는다.
    /// 컴파일 심볼이 아니라 에셋 값이라 QA·라이브 빌드에서도 토글할 수 있다.
    /// </summary>
    public sealed class DebugConfig : GlobalConfig<DebugConfig>
    {
        [Tooltip("끄면 DebugBootstrap 자체가 등록되지 않는다.")]
        public bool EnableDebugUI = true;

        [Tooltip("여기 적힌 IDebugModule.Key 는 개별로 건너뛴다.")]
        public string[] DisabledModuleKeys = Array.Empty<string>();

        public bool IsModuleEnabled(string key)
        {
            if (null == DisabledModuleKeys)
            {
                return true;
            }

            foreach (string disabled in DisabledModuleKeys)
            {
                if (string.Equals(disabled, key, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
