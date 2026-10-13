using System.Collections.Generic;
using System.Linq;
using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.UI
{
    [RequireComponent(typeof(CheatView))]
    public sealed partial class CheatContext : ContextBase<CheatView, CheatVM>
    {
        [Inject] private IEnumerable<ICheatProvider> _providers;

        protected override CheatVM CreateModel()
        {
            return new(_providers.ToList());
        }
    }
}
