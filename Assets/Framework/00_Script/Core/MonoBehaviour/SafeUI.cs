using Cysharp.Threading.Tasks;
using O2un.Roslyn.Generator;
using UnityEngine;

namespace O2un
{
    public abstract partial class SafeUI : SafeMono
    {
        [RequireComponentField] private CanvasGroup _canvasGroup;

        [SerializeField] private bool _isVisibleOnInit = false;
        public bool IsVisible { get; private set; } = false;
        protected override async UniTask Init()
        {
            await base.Init();
            SetVisibleState(_isVisibleOnInit);
        }

        private void SetVisibleState(bool isVisible)
        {
            IsVisible = isVisible;

            if (CanvasGroup == null) return;

            CanvasGroup.alpha = isVisible ? 1f : 0f;
            CanvasGroup.interactable = isVisible;
            CanvasGroup.blocksRaycasts = isVisible;
        }
        
        public async UniTask TurnOnAsync()
        {
            await WaitUntilReadyAsync();

            if(IsVisible)
            {
                return;
            }

            SetVisibleState(true);

            await TurnOnAnim();
        }

        protected virtual async UniTask TurnOnAnim()
        {
            await UniTask.CompletedTask;
        }

        public async UniTask TurnOffAsync()
        {
            await WaitUntilReadyAsync();

            if (false == IsVisible)
            {
                return;   
            }
            
            if (CanvasGroup != null)
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;
            }

            await TurnOffAnim();

            SetVisibleState(false);
        }

        protected virtual async UniTask TurnOffAnim()
        {
            await UniTask.CompletedTask;
        }
    }
}
