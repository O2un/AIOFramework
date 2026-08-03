using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.UI
{
    [RequireComponent(typeof(RoomView))]
    public sealed partial class RoomContext : ContextBase<RoomView, RoomVM>
    {
        [Inject] private IMultiplayerManager _multiplayer;

        protected override RoomVM CreateModel()
        {
            return new(_multiplayer);
        }
    }
}
