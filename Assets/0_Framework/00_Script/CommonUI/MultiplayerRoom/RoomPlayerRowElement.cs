using O2un.Core.Network;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class RoomPlayerRowElement : VisualElement
    {
        private readonly Label _nameLabel;
        private readonly Label _badgeLabel;

        public RoomPlayerRowElement()
        {
            AddToClassList("room-player-row");

            _nameLabel = new Label();
            _nameLabel.AddToClassList("room-player-name");

            _badgeLabel = new Label();
            _badgeLabel.AddToClassList("room-player-badge");
            _badgeLabel.text = "HOST";

            Add(_nameLabel);
            Add(_badgeLabel);
        }

        public void Bind(PlayerInfo player, bool isLocal)
        {
            _nameLabel.text = true == isLocal ? $"{player.PlayerId} (나)" : player.PlayerId;

            EnableInClassList("local", isLocal);
            _badgeLabel.EnableInClassList("u-invisible", false == player.IsHost);
        }
    }
}
