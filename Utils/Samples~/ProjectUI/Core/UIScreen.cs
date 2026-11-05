using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectUI
{
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] private int _order;
        [SerializeField] private ScreenType _screenType = ScreenType.Screen;
        [SerializeField] private ScreenInitMode _initMode = ScreenInitMode.Auto;

        public ScreenInitMode InitMode => _initMode;
        public int Order => _order;
        public ScreenType ScreenType => _screenType;
        
        public event Action OnOpen;
        public event Action OnClose;
        
        public ScreenState State { get; private set; } = ScreenState.Closed;
        public bool IsOpen => State is ScreenState.Opened or ScreenState.Opening;
        public bool IsTransitioning => State is ScreenState.Opening or ScreenState.Closing;
        public bool IsFullyOpen => State == ScreenState.Opened;
        public bool IsFullyClosed => State == ScreenState.Closed;

        public bool IsBlocked => IsSetup && !canvasGroup.interactable;
        public bool IsSetup { get; private set; }
        public bool IsInitialized { get; private set; }
        
        private GraphicRaycaster graphicRaycaster;
        private CanvasGroup canvasGroup;
        private Canvas screenCanvas;
        private ScreenManager manager;

        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
        public CanvasGroup CanvasGroup => canvasGroup;
        public Canvas ScreenCanvas => screenCanvas;
        public ScreenManager Manager => manager;

        protected virtual void OnSetup() { }
        protected virtual void OnInitialize() { }
        protected virtual void OnBlocked() { }
        protected virtual void OnUnblocked() { }
        protected virtual void OnBeforeOpen() { }
        protected virtual void OnBeforeClose() { }
        protected virtual void OnAfterOpen() { }
        protected virtual void OnAfterClose() { }
        public bool CanOpen() => IsSetup && IsInitialized
            && State is ScreenState.Closed;
        public bool CanClose() => IsSetup && IsInitialized
            && State is ScreenState.Opened;

        public void Setup(ScreenManager owner)
        {
            if (IsSetup) return;

            manager = owner;

            canvasGroup = GetComponent<CanvasGroup>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            screenCanvas = GetComponent<Canvas>();

            ApplySorting();

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = true;
            State = ScreenState.Closed;


            if (gameObject.activeSelf)
                gameObject.SetActive(false);

            IsSetup = true;
            OnSetup();
        }

        public void Initialize()
        {
            if (!IsSetup)
            {
                Debug.LogError($"Screen <{GetType().Name}> is not set up, Initialize skipped.");
                return;
            }

            if (IsInitialized) return;

            IsInitialized = true;
            OnInitialize();
        }

        #region Open / Close

        public bool Open()
        {
            // Открытие во время анимации закрытия: закрытие доводится мгновенно, затем обычное открытие.
            if (State == ScreenState.Closing) CompleteTransition();
            if (!CanOpen()) return false;

            State = ScreenState.Opening;

            gameObject.SetActive(true);
            // Сортировка вложенного холста применяется только к включённому объекту: в Setup экран мог быть
            // выключен в сцене, и порядок _order терялся — экраны рисовались по порядку в иерархии.
            ApplySorting();
            BlockRaycasts(false);
            OnBeforeOpen();

            PlayOpenTransition(FinishOpen);
            return true;
        }

        public bool Close()
        {
            // Закрытие во время анимации открытия: открытие доводится мгновенно, затем обычное закрытие.
            if (State == ScreenState.Opening) CompleteTransition();
            if (!CanClose()) return false;

            State = ScreenState.Closing;

            BlockRaycasts(false);
            OnBeforeClose();

            PlayCloseTransition(FinishClose);
            return true;
        }

        private void FinishOpen()
        {
            if (State != ScreenState.Opening) return;

            State = ScreenState.Opened;

            BlockRaycasts(true);
            OnAfterOpen();
            OnOpen?.Invoke();
        }

        private void FinishClose()
        {
            if (State != ScreenState.Closing) return;

            State = ScreenState.Closed;

            OnAfterClose();
            OnClose?.Invoke();

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        #endregion

        #region  Transitions

        protected virtual void PlayOpenTransition(Action onComplete)
        {
            onComplete();
        }

        protected virtual void PlayCloseTransition(Action onComplete)
        {
            onComplete();
        }

        /// <summary>Остановить идущую анимацию и сразу показать её конечное состояние (без вызова onComplete).</summary>
        protected virtual void StopTransition() { }

        private void CompleteTransition()
        {
            StopTransition();
            if (State == ScreenState.Opening) FinishOpen();
            else if (State == ScreenState.Closing) FinishClose();
        }
        #endregion

        #region Block

        public void BlockScreen()
        {
            if (!IsSetup || IsBlocked || !IsFullyOpen)
                return;

            canvasGroup.interactable = false;
            OnBlocked();
        }

        public void UnblockScreen()
        {
            if (!IsSetup || !IsBlocked || !IsFullyOpen)
                return;

            canvasGroup.interactable = true;
            OnUnblocked();
        }

        #endregion

        protected void SetVisualState(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }

        private void ApplySorting()
        {
            screenCanvas.overrideSorting = true;
            screenCanvas.sortingOrder = _order;
        }

        protected void BlockRaycasts(bool block)
        {
            canvasGroup.blocksRaycasts = block;
        }

        /// <summary>Safe cleanup hook for derived screens; do not declare OnDestroy in derived classes.</summary>
        protected virtual void OnScreenDestroyed() { }

        private void OnDestroy()
        {
            OnScreenDestroyed();
            OnOpen = null;
            OnClose = null;

            if (manager != null)
                manager.UnregisterScreen(this);

            manager = null;
        }

        public enum ScreenState
        {
            Closed = 0,
            Opening = 1,
            Opened = 2,
            Closing = 3,
        }
    }

    public enum ScreenInitMode
    {
        Auto = 0,   // initialized by the manager during its init phase
        Manual = 1, // never initialized automatically, owner code decides when
    }
}
