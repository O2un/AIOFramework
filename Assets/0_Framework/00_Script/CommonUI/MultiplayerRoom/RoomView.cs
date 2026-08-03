using System;
using O2un.Core;
using O2un.Core.Network;
using O2un.Core.Utils;
using O2un.MVVM;
using R3;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class RoomView : ViewBaseToolkit<RoomVM>
    {
        private PlayerInfo[] _currentPlayers = Array.Empty<PlayerInfo>();

        private ListView _playerList;
        private Label _roomCodeLabel;
        private Label _roomRoleLabel;
        private Label _playerCountLabel;
        private Button _startGameButton;
        private Button _leaveRoomButton;

        protected override void BindElements(VisualElement root)
        {
            _startGameButton = root.QRequiredBinding<Button>("StartGameButton")
                                   .Clicked(() => Model.StartGame())
                                   .AddTo(DisposableR3).Element;

            _leaveRoomButton = root.QRequiredBinding<Button>("LeaveRoomButton")
                                   .Clicked(() => Model.LeaveRoom())
                                   .AddTo(DisposableR3).Element;

            _roomCodeLabel = root.QRequired<Label>("RoomCodeLabel");
            _roomRoleLabel = root.QRequired<Label>("RoomRoleLabel");
            _playerCountLabel = root.QRequired<Label>("PlayerCountLabel");
            _playerList = root.QRequired<ListView>("PlayerList");

            BindListView();
        }

        protected override void BindModel()
        {
            Model.RoomCode.Subscribe(OnRoomCodeChanged).AddTo(DisposableR3);
            Model.Players.Subscribe(OnPlayersChanged).AddTo(DisposableR3);
            Model.IsHost.Subscribe(OnHostChanged).AddTo(DisposableR3);
            Model.IsBusy.Subscribe(OnBusyChanged).AddTo(DisposableR3);
        }

        private void BindListView()
        {
            _playerList.fixedItemHeight = 52f;
            _playerList.selectionType = SelectionType.None;
            _playerList.makeItem = MakePlayerRow;
            _playerList.bindItem = BindPlayerRow;
        }

        private VisualElement MakePlayerRow()
        {
            return new RoomPlayerRowElement();
        }

        private void BindPlayerRow(VisualElement element, int index)
        {
            if (index < 0) return;
            if (_currentPlayers.Length <= index) return;
            if (element is not RoomPlayerRowElement row) return;

            PlayerInfo player = _currentPlayers[index];

            row.Bind(player, Model.LocalPlayerId == player.PlayerId);
        }

        private void OnRoomCodeChanged(string roomCode)
        {
            _roomCodeLabel.text = true == string.IsNullOrEmpty(roomCode) ? "----" : roomCode;
        }

        private void OnPlayersChanged(PlayerInfo[] players)
        {
            _currentPlayers = players ?? Array.Empty<PlayerInfo>();

            _playerList.itemsSource = _currentPlayers;
            _playerList.Rebuild();

            _playerCountLabel.text = $"{_currentPlayers.Length}";
        }

        private void OnHostChanged(bool isHost)
        {
            _roomRoleLabel.text = true == isHost ? "HOST" : "CLIENT";
            _startGameButton.EnableInClassList("u-invisible", false == isHost);
        }

        private void OnBusyChanged(bool isBusy)
        {
            _startGameButton.SetEnabled(false == isBusy);
            _leaveRoomButton.SetEnabled(false == isBusy);
        }
    }
}
