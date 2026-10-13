using System;
using O2un.Core;
using O2un.Core.Utils;
using O2un.MVVM;
using R3;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class CheatView : ViewBaseToolkit<CheatVM>
    {
        private const string SECTION_CLASS = "cheat-section";
        private const string SECTION_TITLE_CLASS = "cheat-section-title";

        private VisualElement _body;

        protected override void BindElements(VisualElement root)
        {
            _body = root.QRequired<VisualElement>("CheatBody");
            root.QRequiredBinding<Button>("CheatClose").Clicked(() => Model.Hide()).AddTo(DisposableR3);
        }

        protected override void BindModel()
        {
            foreach (ICheatProvider provider in Model.Providers)
            {
                var section = new VisualElement();
                section.AddToClassList(SECTION_CLASS);

                var title = new Label(provider.Title);
                title.AddToClassList(SECTION_TITLE_CLASS);
                section.Add(title);
                _body.Add(section);

                try
                {
                    provider.Build(new CheatPage(section, DisposableR3));
                }
                catch (Exception e)
                {
                    Log.Print(Log.LogLevel.Error, $"[CheatView] 치트 단락을 그리지 못했습니다. provider={provider.GetType().Name}, error={e}");
                }
            }
        }
    }
}
