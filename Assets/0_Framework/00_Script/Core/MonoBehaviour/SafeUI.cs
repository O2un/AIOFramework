using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Roslyn.Analyzer;
using O2un.Roslyn.Generator;
using O2un.Utils;
using R3;
using UnityEngine;

namespace O2un.Core
{
    public enum UITransitionState
    {
        Hidden,
        Showing,
        Shown,
        Hiding
    }

    public abstract partial class SafeUI : SafeMono, ISafeInitializable
    {
        [RequireComponentField] private CanvasGroup _canvasGroup;

        [SerializeField] protected bool _isVisibleOnInit = false;

        private readonly Subject<Unit> _onTurnOnAfter = new();
        private readonly Subject<Unit> _onTurnOffAfter = new();
        public Observable<Unit> OnTurnOnAfter => _onTurnOnAfter;
        public Observable<Unit> OnTurnOffAfter => _onTurnOffAfter;

        private bool _desiredVisible;
        private UITransitionState _transitionState = UITransitionState.Hidden;

        public UITransitionState TransitionState => _transitionState;
        public bool DesiredVisible => _desiredVisible;
        public bool IsShown => UITransitionState.Shown == _transitionState;

        private const string TRANSITION_KEY = "UI_Transition";

        protected override async UniTask Init(CancellationToken ct)
        {
            await base.Init(ct);

            _desiredVisible = _isVisibleOnInit;
            _transitionState = _isVisibleOnInit ? UITransitionState.Shown : UITransitionState.Hidden;
            SetVisibleState(_isVisibleOnInit);
        }

        private void SetVisibleState(bool isVisible)
        {
            if (null == CanvasGroup) return;

            CanvasGroup.alpha = isVisible ? 1f : 0f;
            CanvasGroup.interactable = isVisible;
            CanvasGroup.blocksRaycasts = isVisible;
        }

        public async UniTask TurnOnAsync()
        {
            await WaitUntilReadyAsync();

            _desiredVisible = true;

            if (UITransitionState.Shown == _transitionState || UITransitionState.Showing == _transitionState)
            {
                return;
            }
            _transitionState = UITransitionState.Showing;

            if (null != CanvasGroup)
            {
                CanvasGroup.blocksRaycasts = true;
                CanvasGroup.interactable = false;
            }

            await this.StartExclusiveAsync(TRANSITION_KEY, async ct =>
            {
                SetVisibleState(true);
                await TurnOnAnim(ct);
                ct.ThrowIfCancellationRequested();

                if (false == _desiredVisible)
                {
                    return;
                }

                _transitionState = UITransitionState.Shown;
                if (null != CanvasGroup)
                {
                    CanvasGroup.blocksRaycasts = true;
                    CanvasGroup.interactable = true;
                }
                _onTurnOnAfter.OnNext(Unit.Default);
            });
        }

        protected virtual async UniTask TurnOnAnim(CancellationToken ct)
        {
            await UniTask.CompletedTask;
        }

        public async UniTask TurnOffAsync()
        {
            await WaitUntilReadyAsync();

            _desiredVisible = false;

            if (UITransitionState.Hidden == _transitionState || UITransitionState.Hiding == _transitionState)
            {
                return;
            }
            _transitionState = UITransitionState.Hiding;

            if (null != CanvasGroup)
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;
            }

            await this.StartExclusiveAsync(TRANSITION_KEY, async ct =>
            {
                await TurnOffAnim(ct);
                ct.ThrowIfCancellationRequested();

                if (_desiredVisible)
                {
                    return;
                }

                SetVisibleState(false);
                _transitionState = UITransitionState.Hidden;
                _onTurnOffAfter.OnNext(Unit.Default);
            });
        }

        protected virtual async UniTask TurnOffAnim(CancellationToken ct)
        {
            await UniTask.CompletedTask;
        }

        public async UniTask Switch(bool isOn)
        {
            if (isOn)
            {
                await TurnOnAsync();
            }
            else
            {
                await TurnOffAsync();
            }
        }

        [MustCallBase]
        protected override void SafeDestroy()
        {
            _onTurnOnAfter.Dispose();
            _onTurnOffAfter.Dispose();

            base.SafeDestroy();
        }
    }
}
