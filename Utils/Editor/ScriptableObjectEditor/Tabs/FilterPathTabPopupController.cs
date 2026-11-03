using System;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class FilterPathTabPopupController
    {
        private readonly ScriptableObjectFilterPathTabs _filterPathTabs;
        private readonly Action _onFilterPathChanged;
        private readonly Action _repaint;
        private FilterPathTabPopupRequest _pendingPopup;

        public FilterPathTabPopupController(
            ScriptableObjectFilterPathTabs filterPathTabs,
            Action onFilterPathChanged,
            Action repaint)
        {
            _filterPathTabs = filterPathTabs;
            _onFilterPathChanged = onFilterPathChanged;
            _repaint = repaint;
        }

        public void ShowAddPopup(Rect activatorRect)
        {
            ShowPopup(CreateAddRequest(activatorRect));
        }

        public void QueueAddPopup(Rect activatorRect)
        {
            _pendingPopup = CreateAddRequest(activatorRect);
            _repaint?.Invoke();
        }

        public void QueueEditPopup(int tabIndex, Rect activatorRect)
        {
            if (tabIndex < 0 || tabIndex >= _filterPathTabs.Tabs.Count)
                return;

            var tab = _filterPathTabs.Tabs[tabIndex];
            _pendingPopup = new FilterPathTabPopupRequest(
                activatorRect,
                "Edit Filter Path",
                tab.Name,
                tab.Path,
                (name, path) =>
                {
                    if (!_filterPathTabs.Edit(tabIndex, name, path))
                        return;

                    _onFilterPathChanged?.Invoke();
                });

            _repaint?.Invoke();
        }

        public void RemoveTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _filterPathTabs.Tabs.Count)
                return;

            var tab = _filterPathTabs.Tabs[tabIndex];
            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Filter Path",
                $"Remove '{tab.Name}'?",
                "Remove",
                "Cancel");

            if (!confirmed)
                return;

            if (!_filterPathTabs.Remove(tabIndex))
                return;

            _onFilterPathChanged?.Invoke();
        }

        public void ShowTabContextMenu(int tabIndex, Rect activatorRect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Edit"), false, () => QueueEditPopup(tabIndex, activatorRect));

            if (_filterPathTabs.Tabs.Count <= 1)
            {
                menu.AddDisabledItem(new GUIContent("Delete"));
            }
            else
            {
                menu.AddItem(new GUIContent("Delete"), false, () => RemoveTab(tabIndex));
            }

            menu.ShowAsContext();
        }

        public void ShowPanelContextMenu(Rect activatorRect, Action refreshList, Action closeWindow)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Tab"), false, () => QueueAddPopup(activatorRect));
            menu.AddItem(new GUIContent("Refresh"), false, () => refreshList?.Invoke());
            menu.AddItem(new GUIContent("Close Panel"), false, () => closeWindow?.Invoke());
            menu.ShowAsContext();
        }

        public void DrawPendingPopup()
        {
            if (_pendingPopup == null || Event.current.type != EventType.Repaint)
                return;

            var popup = _pendingPopup;
            _pendingPopup = null;
            ShowPopup(popup);
        }

        private FilterPathTabPopupRequest CreateAddRequest(Rect activatorRect)
        {
            return new FilterPathTabPopupRequest(
                activatorRect,
                "Add Filter Path",
                "New Tab",
                _filterPathTabs.SelectedPath,
                (name, path) =>
                {
                    if (!_filterPathTabs.Add(name, path))
                        return;

                    _onFilterPathChanged?.Invoke();
                });
        }

        private static void ShowPopup(FilterPathTabPopupRequest popup)
        {
            PopupWindow.Show(
                popup.ActivatorRect,
                new FilterPathTabPopup(popup.Title, popup.Name, popup.Path, popup.OnSave));
        }

        private class FilterPathTabPopupRequest
        {
            public Rect ActivatorRect { get; }
            public string Title { get; }
            public string Name { get; }
            public string Path { get; }
            public Action<string, string> OnSave { get; }

            public FilterPathTabPopupRequest(
                Rect activatorRect,
                string title,
                string name,
                string path,
                Action<string, string> onSave)
            {
                ActivatorRect = activatorRect;
                Title = title;
                Name = name;
                Path = path;
                OnSave = onSave;
            }
        }
    }
}
