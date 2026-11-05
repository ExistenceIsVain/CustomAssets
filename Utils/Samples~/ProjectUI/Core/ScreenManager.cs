using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectUI
{
    public class ScreenManager : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private Transform screenPanelParent;
        [SerializeField] private Transform overlayPanelParent;

        public RectTransform OverlayContainerRect => overlayPanelParent as RectTransform;
        public RectTransform ScreensContainerRect => screenPanelParent as RectTransform;
        public Canvas Canvas => canvas;
        public CanvasScaler CanvasScaler => canvasScaler;
        public Transform ScreenPanelsTransform => screenPanelParent;
        public Transform OverlayPanelsTransform => overlayPanelParent;

        private readonly Dictionary<Type, UIScreen> screenMap = new();
        private readonly List<UIScreen> allScreens = new();
        private readonly List<UIScreen> buffer = new();

        private readonly ScreenHistory openHistory = new();
        private readonly ScreenHistory closedHistory = new();

        #region Init

        private void Awake()
        {
            InitializeScreens();
        }

        private void InitializeScreens()
        {
            ClearHistory();
            CollectScreens(screenPanelParent);
            CollectScreens(overlayPanelParent);

            for (int i = 0; i < allScreens.Count; i++)
            {
                var screen = allScreens[i];
                if (!screen.IsInitialized && screen.InitMode == ScreenInitMode.Auto)
                    screen.Initialize();
            }
        }

        public bool InitializeScreen<T>() where T : UIScreen
        {
            var screen = GetScreen<T>();
            if (screen == null)
                return false;

            screen.Initialize();
            return screen.IsInitialized;
        }

        private void CollectScreens(Transform parent)
        {
            if (parent == null) return;

            buffer.Clear();
            parent.GetComponentsInChildren(true, buffer);

            for (int i = 0; i < buffer.Count; i++)
                RegisterScreen(buffer[i]);

            buffer.Clear();
        }

        public bool RegisterScreen(UIScreen screen)
        {
            if (screen == null) return false;

            var type = screen.GetType();
            if (screenMap.TryGetValue(type, out var existing))
            {
                if (existing == screen) return true;

                Debug.LogWarning($"Screen <{type.Name}> already registered, skipping duplicate.");
                return false;
            }

            screenMap[type] = screen;
            allScreens.Add(screen);

            if(!screen.IsSetup)
                screen.Setup(this);

            return true;
        }

        public void UnregisterScreen(UIScreen screen)
        {
            if (screen == null) return;

            var type = screen.GetType();
            if (screenMap.TryGetValue(type, out var existing) && existing == screen)
                screenMap.Remove(type);

            allScreens.Remove(screen);
            // openHistory.Remove(screen);
            // closedHistory.Remove(screen);
        }

        #endregion

        #region Open / Close

        public void OpenScreen<T>() where T : UIScreen
        {
            var screen = GetScreen<T>();
            if (screen == null) return;

            OpenScreen(screen);
        }

        public void OpenScreen<T, TArg>(TArg arg) where T : UIScreen, IAcceptArg<TArg>
        {
            var screen = GetScreen<T>();
            if (screen == null) return;

            screen.AcceptArg(arg);
            OpenScreen(screen);
        }

        public void CloseScreen<T>() where T : UIScreen
        {
            var screen = GetScreen<T>();
            if (screen == null) return;

            CloseScreen(screen);
        }

        public void CloseAllType(ScreenType type)
        {
            for (int i = allScreens.Count - 1; i >= 0; i--)
            {
                var screen = allScreens[i];
                if (screen.ScreenType == type && screen.IsOpen)
                {
                    CloseScreen(screen);
                }
            }
        }

        public void CloseAll()
        {
            for (int i = allScreens.Count - 1; i >= 0; i--)
            {
                CloseScreen(allScreens[i]);
            }
        }

        public bool GoBack()
        {
            if (openHistory.Count < 2) return false;

            // skip current
            var current = openHistory.Pop();
            var previous = openHistory.Peek();

            CloseScreen(current);
            OpenScreen(previous);
            return true;
        }
        
        private void OpenScreen(UIScreen screen)
        {
            if (screen == null)
                return;

            if (screen.Open())
                AddToOpenHistory(screen);
        }

        private void CloseScreen(UIScreen screen)
        {
            if (screen == null)
                return;

            if (screen.Close())
                AddToCloseHistory(screen);
        }

        #endregion

        #region Block

        public void BlockScreen<T>() where T : UIScreen
        {
            var screen = GetScreen<T>();
            if (screen) screen.BlockScreen();
        }

        public void UnblockScreen<T>() where T : UIScreen
        {
            var screen = GetScreen<T>();
            if (screen) screen.UnblockScreen();
        }

        public void BlockAllExcept<T>() where T : UIScreen
        {
            var exception = GetScreen<T>();
            foreach (var screen in allScreens)
            {
                if (screen != exception)
                    screen.BlockScreen();
            }
        }

        //screenManager.BlockAllExcept(typeof(HudScreen), typeof(ShopScreen));
        public void BlockAllExcept(params Type[] screenTypes)
        {
            foreach (var screen in allScreens)
            {
                if (Array.IndexOf(screenTypes, screen.GetType()) < 0)
                    screen.BlockScreen();
            }
        }
        
        public void BlockAll()
        {
            foreach (var screen in allScreens)
                screen.BlockScreen();
        }

        public void UnblockAll()
        {
            foreach (var screen in allScreens)
                screen.UnblockScreen();
        }

        #endregion

        #region History
        public void ClearHistory()
        {
            openHistory.Clear();
            closedHistory.Clear();
        }

        public void AddToOpenHistory(UIScreen screen)
        {
            openHistory.Add(screen);
        }

        // Generic versions for convenience
        public bool LastOpenedNoneOf<T>(int count) where T : UIScreen
        {
            return LastOpenedNoneOf(count, typeof(T));
        }

        public bool LastOpenedAnyOf<T>(int count) where T : UIScreen
        {
            return LastOpenedAnyOf(count, typeof(T));
        }

        // Check if none of the last N opened screens match the given types
        public bool LastOpenedNoneOf(int count, params Type[] screenTypes)
        {
            return openHistory.NoneOf(count, screenTypes);
        }

        // Check if ALL of the last N opened screens match the given types
        public bool LastOpenedAllOf(int count, params Type[] screenTypes)
        {
            return openHistory.AllOf(count, screenTypes);
        }

        // Check if any of the last N opened screens match the given types
        public bool LastOpenedAnyOf(int count, params Type[] screenTypes)
        {
            return openHistory.AnyOf(count, screenTypes);
        }

        public void AddToCloseHistory(UIScreen screen)
        {
            closedHistory.Add(screen);
        }

        //Single generic versions for convenience
        public bool LastClosedNoneOf<T>(int count) where T : UIScreen
        {
            return LastClosedNoneOf(count, typeof(T));
        }

        public bool LastClosedAnyOf<T>(int count) where T : UIScreen
        {
            return LastClosedAnyOf(count, typeof(T));
        }

        // Check if none of the last N closed screens match the given types
        public bool LastClosedNoneOf(int count, params Type[] screenTypes)
        {
            return closedHistory.NoneOf(count, screenTypes);
        }

        // Check if any of the last N closed screens match the given types
        public bool LastClosedAnyOf(int count, params Type[] screenTypes)
        {
            return closedHistory.AnyOf(count, screenTypes);
        }

        // Check if ALL of the last N closed screens match the given types
        public bool LastClosedAllOf(int count, params Type[] screenTypes)
        {
            return closedHistory.AllOf(count, screenTypes);
        }

        #endregion

        #region Query

        public T GetScreen<T>() where T : UIScreen
        {
            if (screenMap.TryGetValue(typeof(T), out var screen) && screen != null)
                return (T)screen;

            Debug.LogError($"Screen <{typeof(T).Name}> not found!");
            return null;
        }

        public bool IsScreenOpen<T>() where T : UIScreen
        {
            if (screenMap.TryGetValue(typeof(T), out var screen))
                return screen.IsOpen;

            return false;
        }

        public bool IsScreenBlocked<T>() where T : UIScreen
        {
            if (screenMap.TryGetValue(typeof(T), out var screen))
                return screen.IsBlocked;

            return false;
        }

        public bool TryGetScreen<T>(out T screen) where T : UIScreen
        {
            if (screenMap.TryGetValue(typeof(T), out var found) && found != null)
            {
                screen = (T)found;
                return true;
            }

            screen = null;
            return false;
        }

        private int Count(Func<UIScreen, bool> predicate)
        {
            int count = 0;
            foreach (var s in allScreens)
                if (predicate(s)) count++;
            return count;
        }

        public int ActiveCount => Count(s => s.IsOpen);
        public int BlockedCount => Count(s => s.IsBlocked);
        public int TransitioningCount => Count(s => s.IsTransitioning);

        #endregion
    }

    public enum ScreenType
    {
        Screen = 0,
        Overlay = 1,
    }
}
