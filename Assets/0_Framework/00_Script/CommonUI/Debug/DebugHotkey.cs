using System;
using R3;
using UnityEngine.InputSystem;

namespace O2un.UI
{
    public static class DebugHotkey
    {
        private const float DOUBLE_PRESS_SECONDS = 1f;

        // Ctrl+Alt+키 를 1초 안에 두 번 눌러야 신호가 난다. 오작동으로 디버그 창이 열리는 것을 막기 위함.
        public static Observable<Unit> CtrlAltDoublePress(Key key)
        {
            var comboStream = Observable.EveryUpdate().Where(_ => IsPressed(key));

            return comboStream
                   .Select(_ => comboStream.Take(1).TakeUntil(Observable.Timer(TimeSpan.FromSeconds(DOUBLE_PRESS_SECONDS))))
                   .Switch();
        }

        private static bool IsPressed(Key key)
        {
            Keyboard keyboard = Keyboard.current;

            if (null == keyboard) return false;
            if (false == keyboard.ctrlKey.isPressed) return false;
            if (false == keyboard.altKey.isPressed) return false;

            return keyboard[key].wasPressedThisFrame;
        }
    }
}
