using System;
using System.Collections;
using System.Collections.Generic;
using O2un.Core;
using O2un.Core.Network;
using O2un.Core.Utils;
using O2un.MVVM;
using R3;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class MultiplayerEntryView : ViewBaseToolkit<MultiplayerEntryVM>
    {
        private IReadOnlyList<RoomSummary> _currentRooms = Array.Empty<RoomSummary>();

        private ListView _roomList;
        private TextField _roomCodeField;
        private VisualElement _roomListEmpty;
        private Button _createRoomButton;
        private Button _joinRoomButton;
        private Button _refreshButton;

        protected override void BindElements(VisualElement root)
        {
            _createRoomButton = root.QRequiredBinding<Button>("CreateRoomButton")
                                    .Clicked(() => Model.CreateRoom())
                                    .AddTo(DisposableR3).Element;

            _joinRoomButton = root.QRequiredBinding<Button>("JoinRoomButton")
                                  .Clicked(() => Model.JoinRoom())
                                  .AddTo(DisposableR3).Element;

            _refreshButton = root.QRequiredBinding<Button>("RefreshRoomListButton")
                                 .Clicked(() => Model.RefreshRoomList())
                                 .AddTo(DisposableR3).Element;

            _roomCodeField = root.QRequiredBinding<TextField>("RoomCodeField")
                                 .ValueChangedValue<string>(s => Model.SetRoomCode(s))
                                 .AddTo(DisposableR3).Element;

            _roomList = root.QRequiredBinding<ListView>("RoomList")
                            .SelectChanged(OnRoomSelectChanged)
                            .AddTo(DisposableR3).Element;

            _roomListEmpty = root.QRequired<VisualElement>("RoomListEmpty");

            BindListView();
        }

        protected override void BindModel()
        {
            Model.Rooms.Subscribe(OnRoomsChanged).AddTo(DisposableR3);
            Model.RoomCode.Subscribe(OnRoomCodeChanged).AddTo(DisposableR3);
            Model.IsBusy.Subscribe(OnBusyChanged).AddTo(DisposableR3);
        }

        private void BindListView()
        {
            _roomList.fixedItemHeight = 52f;
            _roomList.selectionType = SelectionType.Single;
            _roomList.makeItem = MakeRoomRow;
            _roomList.bindItem = BindRoomRow;
        }

        private VisualElement MakeRoomRow()
        {
            return new RoomRowElement();
        }

        private void BindRoomRow(VisualElement element, int index)
        {
            if (index < 0)
            {
                return;
            }

            if (_currentRooms.Count <= index)
            {
                return;
            }

            if (element is not RoomRowElement row)
            {
                return;
            }

            row.Bind(_currentRooms[index]);
        }

        private void OnRoomsChanged(IReadOnlyList<RoomSummary> rooms)
        {
            _currentRooms = rooms ?? Array.Empty<RoomSummary>();

            _roomList.itemsSource = _currentRooms is IList list ? list : new List<RoomSummary>(_currentRooms);
            _roomList.Rebuild();

            _roomListEmpty.EnableInClassList("u-invisible", 0 < _currentRooms.Count);
        }

        private void OnRoomSelectChanged(IEnumerable<object> selectedItems)
        {
            foreach (object selectedItem in selectedItems)
            {
                if (selectedItem is not RoomSummary room)
                {
                    continue;
                }

                Model.SelectRoom(room);
                return;
            }
        }

        // VM 이 입력값을 그대로 되돌려주므로, 알림을 끄지 않으면 한 글자마다 변경 이벤트가 되돈다.
        private void OnRoomCodeChanged(string roomCode)
        {
            _roomCodeField.SetValueWithoutNotify(roomCode ?? string.Empty);
        }

        private void OnBusyChanged(bool isBusy)
        {
            _createRoomButton.SetEnabled(false == isBusy);
            _joinRoomButton.SetEnabled(false == isBusy);
            _refreshButton.SetEnabled(false == isBusy);
        }
    }
}
