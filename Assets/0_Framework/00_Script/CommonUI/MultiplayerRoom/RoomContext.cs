using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.UI
{
    [RequireComponent(typeof(RoomView))]
    public sealed partial class RoomContext : ContextBase<RoomView, RoomVM>
    {
        [Inject] private IMultiplayerManager _multiplayer;
        [Inject] private GameStartRunner _gameStart;

        protected override RoomVM CreateModel()
        {
            return new(_multiplayer, _gameStart);
        }
    }
}
