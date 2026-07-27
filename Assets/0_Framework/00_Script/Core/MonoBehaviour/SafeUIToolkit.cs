using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Roslyn.Analyzer;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace O2un.Core
{
    /// <summary>
    /// Runtime UI Toolkit counterpart of SafeUI.
    ///
    /// Visibility mapping:
    /// - CanvasGroup.alpha        -> VisualElement.style.opacity
    /// - CanvasGroup.interactable -> VisualElement.SetEnabled
    /// - LayoutElement.ignoreLayout -> DisplayStyle.None when hidden
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract partial class SafeUIToolkit : SafeMono, ISafeInitializable
    {
        [SerializeField] private UIDocument _uiDocument;

        [Tooltip("Empty means UIDocument.rootVisualElement. Otherwise this must match a UXML element name.")]
        [SerializeField] private string _viewElementName;

        [SerializeField] protected bool _isVisibleOnInit;

        [Tooltip("When enabled, hidden UI uses display:none and is removed from flex layout.")]
        [SerializeField] private bool _collapseLayoutWhenHidden = true;

        [Tooltip("Default fade duration. Override TurnOnAnim/TurnOffAnim for custom transitions.")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;

        [SerializeField] private bool _useUnscaledTime = true;

        private readonly Subject<Unit> _onTurnOnAfter = new();
        private readonly Subject<Unit> _onTurnOffAfter = new();

        private CancellationTokenSource _transitionCts;
        private int _transitionVersion;
        private bool _desiredVisible;
        private float _currentOpacity;
        private UITransitionState _transitionState = UITransitionState.Hidden;

        public Observable<Unit> OnTurnOnAfter => _onTurnOnAfter;
        public Observable<Unit> OnTurnOffAfter => _onTurnOffAfter;

        public VisualElement ViewRoot { get; private set; }
        public UITransitionState TransitionState => _transitionState;
        public bool DesiredVisible => _desiredVisible;
        public bool IsShown => _transitionState == UITransitionState.Shown;

        protected UIDocument Document => _uiDocument;
        protected float CurrentOpacity => _currentOpacity;

        protected override async UniTask Init(CancellationToken ct)
        {
            await base.Init(ct);

            _uiDocument ??= GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} requires a {nameof(UIDocument)} component.");
            }

            // UIDocument builds its visual tree during its enable lifecycle. Do not rely on
            // component execution order when SafeMono initializes from Awake/OnEnable.
            await UniTask.WaitUntil(
                () => _uiDocument.rootVisualElement != null,
                PlayerLoopTiming.Update,
                ct);

            VisualElement documentRoot = _uiDocument.rootVisualElement;

            ViewRoot = string.IsNullOrWhiteSpace(_viewElementName)
                ? documentRoot
                : documentRoot.Q(name: _viewElementName);

            if (ViewRoot == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}: UXML element named '{_viewElementName}' was not found.");
            }

            BindElements(ViewRoot);

            _desiredVisible = _isVisibleOnInit;
            _transitionState = _isVisibleOnInit
                ? UITransitionState.Shown
                : UITransitionState.Hidden;

            ApplySettledState(_isVisibleOnInit);
        }

        /// <summary>
        /// Resolve UXML child references here with QRequired/QOptional.
        /// This is the UI Toolkit equivalent of assigning child controls through SerializeField.
        /// </summary>
        protected virtual void BindElements(VisualElement root)
        {
        }

        public UniTask TurnOnAsync()
        {
            return SetVisibleAsync(true);
        }

        public UniTask TurnOffAsync()
        {
            return SetVisibleAsync(false);
        }

        public UniTask SwitchAsync(bool isOn)
        {
            return SetVisibleAsync(isOn);
        }

        private async UniTask SetVisibleAsync(bool isVisible)
        {
            await WaitUntilReadyAsync();

            _desiredVisible = isVisible;

            if (isVisible)
            {
                if (_transitionState == UITransitionState.Shown ||
                    _transitionState == UITransitionState.Showing)
                {
                    return;
                }
            }
            else
            {
                if (_transitionState == UITransitionState.Hidden ||
                    _transitionState == UITransitionState.Hiding)
                {
                    return;
                }
            }

            int version = ++_transitionVersion;
            CancelCurrentTransition();

            var localCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy(),
                CancellationToken.None);
            CancellationToken token = localCts.Token;
            _transitionCts = localCts;

            try
            {
                if (isVisible)
                {
                    await ShowCoreAsync(version, token);
                }
                else
                {
                    await HideCoreAsync(version, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // A newer visibility request superseded this transition, or the object was destroyed.
            }
            catch
            {
                // Never leave the state machine wedged in Showing/Hiding after an animation failure.
                if (version == _transitionVersion && ViewRoot != null)
                {
                    ApplySettledState(_desiredVisible);
                    _transitionState = _desiredVisible
                        ? UITransitionState.Shown
                        : UITransitionState.Hidden;
                }

                throw;
            }
            finally
            {
                if (ReferenceEquals(_transitionCts, localCts))
                {
                    _transitionCts = null;
                    localCts.Dispose();
                }
            }
        }

        private async UniTask ShowCoreAsync(int version, CancellationToken ct)
        {
            _transitionState = UITransitionState.Showing;
            PrepareForShow();

            await TurnOnAnim(ct);
            ct.ThrowIfCancellationRequested();

            if (version != _transitionVersion || !_desiredVisible)
            {
                return;
            }

            ApplySettledState(true);
            _transitionState = UITransitionState.Shown;
            _onTurnOnAfter.OnNext(Unit.Default);
        }

        private async UniTask HideCoreAsync(int version, CancellationToken ct)
        {
            _transitionState = UITransitionState.Hiding;
            PrepareForHide();

            await TurnOffAnim(ct);
            ct.ThrowIfCancellationRequested();

            if (version != _transitionVersion || _desiredVisible)
            {
                return;
            }

            ApplySettledState(false);
            _transitionState = UITransitionState.Hidden;
            _onTurnOffAfter.OnNext(Unit.Default);
        }

        private void PrepareForShow()
        {
            ViewRoot.style.display = DisplayStyle.Flex;
            ViewRoot.style.visibility = Visibility.Visible;
            SetInteractionEnabled(false);
        }

        private void PrepareForHide()
        {
            SetInteractionEnabled(false);
        }

        private void ApplySettledState(bool isVisible)
        {
            SetOpacity(isVisible ? 1f : 0f);
            SetInteractionEnabled(isVisible);

            if (isVisible)
            {
                ViewRoot.style.display = DisplayStyle.Flex;
                ViewRoot.style.visibility = Visibility.Visible;
                return;
            }

            ViewRoot.style.visibility = Visibility.Hidden;
            ViewRoot.style.display = _collapseLayoutWhenHidden
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        /// <summary>
        /// SetEnabled disables input for this element and implicitly for its children.
        /// Override this if the view uses a dedicated input blocker instead.
        /// </summary>
        protected virtual void SetInteractionEnabled(bool enabled)
        {
            ViewRoot.SetEnabled(enabled);
        }

        protected virtual UniTask TurnOnAnim(CancellationToken ct)
        {
            return FadeToAsync(1f, _fadeDuration, ct);
        }

        protected virtual UniTask TurnOffAnim(CancellationToken ct)
        {
            return FadeToAsync(0f, _fadeDuration, ct);
        }

        protected async UniTask FadeToAsync(
            float targetOpacity,
            float duration,
            CancellationToken ct)
        {
            targetOpacity = Mathf.Clamp01(targetOpacity);

            if (duration <= 0f || Mathf.Approximately(_currentOpacity, targetOpacity))
            {
                SetOpacity(targetOpacity);
                return;
            }

            float startOpacity = _currentOpacity;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();

                elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetOpacity(Mathf.LerpUnclamped(startOpacity, targetOpacity, t));

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            SetOpacity(targetOpacity);
        }

        protected void SetOpacity(float opacity)
        {
            _currentOpacity = Mathf.Clamp01(opacity);
            ViewRoot.style.opacity = _currentOpacity;
        }

        private void CancelCurrentTransition()
        {
            CancellationTokenSource cts = _transitionCts;
            _transitionCts = null;

            if (cts == null)
            {
                return;
            }

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }

            cts.Dispose();
        }

        [MustCallBase]
        protected override void SafeDestroy()
        {
            ++_transitionVersion;
            CancelCurrentTransition();

            _onTurnOnAfter.Dispose();
            _onTurnOffAfter.Dispose();

            base.SafeDestroy();
        }
    }
}
