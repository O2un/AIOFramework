using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace O2un.Utils
{
    public static class StringUtils
    {
        public static async UniTask<string> GetLocalizedString(TableReference tableReference, TableEntryReference tableEntryReference, params object[] arguments)
        {
            return await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableReference, tableEntryReference, arguments).ToUniTask();
        }

        public static async UniTask<string> GetLocalizedString(TableReference tableReference, TableEntryReference tableEntryReference, IList<object> arguments)
        {
            return await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableReference, tableEntryReference, arguments).ToUniTask();
        }
    }
}
