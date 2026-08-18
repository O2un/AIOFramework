using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.UI
{
    [RequireComponent(typeof(MultiplayerEntryView))]
    public sealed partial class MultiplayerEntryContext : ContextBase<MultiplayerEntryView, MultiplayerEntryVM>
    {
        [Inject] private IMultiplayerManager _multiplayer;

        protected override MultiplayerEntryVM CreateModel()
        {
            return new(_multiplayer);
        }
    }
}
