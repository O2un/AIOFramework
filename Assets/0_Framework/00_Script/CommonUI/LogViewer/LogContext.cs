using O2un.Core.Utils;
using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.UI
{
    [RequireComponent(typeof(LogView))]
    public sealed partial class LogContext : ContextBase<LogView, LogVM>
    {
        [Inject] private ILogManager _manager;

        protected override LogVM CreateModel()
        {
            return new(_manager);
        }
    }
}
