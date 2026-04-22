using O2un.NVVM;
using TMPro;
using R3;
using UnityEngine.UI;
using UnityEngine;

namespace O2un.UI
{
    public sealed class LoadingView : SafeView<LoadingViewModel>
    {
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;

        protected override void BindModel()
        {
            Model.Progress
            .Subscribe(UpdateProgressUI)
            .AddTo(_disposableR3);
        }

        private void UpdateProgressUI(float progress)
        {
            if (_progressBar != null) _progressBar.fillAmount = progress;
            if (_progressText != null) _progressText.SetText("0",progress*100f);
        }
    }
}
