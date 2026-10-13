using O2un.Core;
using R3;
using UnityEngine;

namespace O2un.UI
{
    public sealed class TimeScaleCheat : SafeDisposableClass, ICheatProvider
    {
        private static readonly float[] SCALES = { 0f, 0.5f, 1f, 2f, 5f, 10f };

        private readonly ReactiveProperty<float> _current = new ReactiveProperty<float>(1f);

        public TimeScaleCheat()
        {
            _current.AddTo(DisposableR3);
        }

        public string Title => "타임스케일";

        public void Build(CheatPage page)
        {
            _current.Value = Time.timeScale;
            page.Status(() => $"현재 ×{_current.Value}", _current.AsUnitObservable());

            foreach (float scale in SCALES)
            {
                float value = scale;
                page.Button($"×{value}", () =>
                {
                    Time.timeScale = value;
                    _current.Value = value;
                });
            }
        }
    }
}
