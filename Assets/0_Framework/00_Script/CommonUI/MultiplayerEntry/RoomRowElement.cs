using O2un.Core.Network;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class RoomRowElement : VisualElement
    {
        private readonly Label _codeLabel;
        private readonly Label _hostLabel;
        private readonly Label _countLabel;
        private readonly Label _badgeLabel;

        public RoomRowElement()
        {
            AddToClassList("room-row");

            _codeLabel = new Label();
            _codeLabel.AddToClassList("room-code");

            _hostLabel = new Label();
            _hostLabel.AddToClassList("room-host");

            _countLabel = new Label();
            _countLabel.AddToClassList("room-count");

            _badgeLabel = new Label();
            _badgeLabel.AddToClassList("room-badge");

            Add(_codeLabel);
            Add(_hostLabel);
            Add(_countLabel);
            Add(_badgeLabel);
        }

        public void Bind(RoomSummary room)
        {
            RemoveFromClassList("full");

            _codeLabel.text = room.RoomCode;
            _hostLabel.text = room.HostPlayerId;
            _countLabel.text = $"{room.CurrentPlayers}/{room.MaxPlayers}";
            _badgeLabel.text = true == room.IsJoinable ? "OPEN" : "FULL";

            if (false == room.IsJoinable)
            {
                AddToClassList("full");
            }
        }
    }
}
