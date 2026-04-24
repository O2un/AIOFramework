using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Roslyn.Generator;
using O2un.Utils;
using UnityEngine;

namespace O2un
{
    public abstract partial class SafeUI : SafeMono
    {
        [RequireComponentField] private CanvasGroup _canvasGroup;

        [SerializeField] private bool _isVisibleOnInit = false;
        public bool IsVisible { get; private set; } = false;
        private const string TRANSITION_KEY = "UI_Transition";
        protected override async UniTask Init(CancellationToken ct)
        {
            await base.Init(ct);
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
            IsVisible = true;

            if (CanvasGroup != null)
            {
                CanvasGroup.blocksRaycasts = true;
                CanvasGroup.interactable = false;
            }

            await this.StartExclusiveAsync(TRANSITION_KEY, async ct => 
            {
                await TurnOnAnim(ct);
                SetVisibleState(true);
            });
        }

        protected virtual async UniTask TurnOnAnim(CancellationToken ct)
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

            await this.StartExclusiveAsync(TRANSITION_KEY, async ct => 
            {
                await TurnOffAnim(ct);
                SetVisibleState(false);
            });
        }

        protected virtual async UniTask TurnOffAnim(CancellationToken ct)
        {
            await UniTask.CompletedTask;
        }
    }
}
