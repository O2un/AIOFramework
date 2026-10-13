using System;
using O2un.Core;
using O2un.Core.Utils;
using R3;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class CheatPage
    {
        private const string ROW_CLASS = "cheat-row";
        private const string BUTTON_CLASS = "cheat-button";
        private const string STATUS_CLASS = "cheat-status";

        private readonly VisualElement _body;
        private readonly CompositeDisposable _lifetime;

        private VisualElement _row;
        private int _buttonSerial;

        internal CheatPage(VisualElement body, CompositeDisposable lifetime)
        {
            _body = body;
            _lifetime = lifetime;
        }

        public void Row()
        {
            _row = null;
        }

        public void Button(string text, Action onClick)
        {
            if (null == _row)
            {
                _row = new VisualElement();
                _row.AddToClassList(ROW_CLASS);
                _body.Add(_row);
            }

            string name = $"CheatButton{++_buttonSerial}";
            var button = new Button { name = name, text = text };
            button.AddToClassList(BUTTON_CLASS);
            _row.Add(button);

            _row.QRequiredBinding<Button>(name).Clicked(onClick).AddTo(_lifetime);
        }

        public void Status(Func<string> read, Observable<Unit> changed)
        {
            var label = new Label();
            label.AddToClassList(STATUS_CLASS);
            _body.Add(label);
            _row = null;

            changed.Prepend(Unit.Default)
                   .Subscribe(_ => label.text = read())
                   .AddTo(_lifetime);
        }
    }
}
