using System;
using R3;

namespace O2un.DEV
{
    /// <summary>
    /// 데모 씬이 지금 어느 페이지를 보여주는지에 대한 정본.
    ///
    /// 허브는 어떤 패널이 존재하는지 모르고 페이지 이름만 던진다. 자기가 그 페이지에 속하는지는
    /// 각 데모 VM 이 판단해 스스로 켜고 끈다. 데모를 추가할 때 허브를 고치지 않아도 되는 이유가 이 방향이다.
    /// </summary>
    public sealed class UIDemoRouter : IDisposable
    {
        private readonly ReactiveProperty<UIDemoPage> _current = new(UIDemoPage.None);

        public ReadOnlyReactiveProperty<UIDemoPage> Current => _current;

        public void Select(UIDemoPage page)
        {
            _current.Value = page;
        }

        public void Dispose()
        {
            _current.Dispose();
        }
    }
}
