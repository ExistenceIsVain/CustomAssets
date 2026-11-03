using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectEditorWindow : EditorWindow
    {
        private readonly ScriptableObjectFilterPathTabs _filterPathTabs = new();
        private readonly ScriptableObjectAssetCatalog _assetCatalog = new();
        private readonly ScriptableObjectListPanel _listPanel = new();
        private readonly ScriptableObjectInspectorPanel _inspectorPanel = new();
        private readonly ScriptableObjectFilterPathTabBar _filterPathTabBar = new();
        private readonly EditorContextClickGuard _contextClickGuard = new();

        private ScriptableObjectAssetCatalogResult _catalogResult;
        private FilterPathTabPopupController _filterPathTabPopupController;
        private ScriptableObject _selectedSettings;
        private UnityEditor.Editor _cachedEditor;
        private bool _clearFieldFocusPending;

        [MenuItem("Tools/Scriptable Object Editor", false, 0)]
        private static void Init()
        {
            var window = (ScriptableObjectEditorWindow)GetWindow(typeof(ScriptableObjectEditorWindow));
            window.titleContent = new GUIContent("SO Editor");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _filterPathTabPopupController = new FilterPathTabPopupController(
                _filterPathTabs,
                OnFilterPathChanged,
                Repaint);

            _filterPathTabs.Load();
            RefreshList();
        }

        private void OnDestroy()
        {
            if (_cachedEditor != null)
                DestroyImmediate(_cachedEditor);
        }

        private void RefreshList()
        {
            _catalogResult = _assetCatalog.Refresh(_filterPathTabs.SelectedPath);
        }

        private void SelectSO(ScriptableObject so)
        {
            _clearFieldFocusPending = true;
            _selectedSettings = so;
            UnityEditor.Editor.CreateCachedEditor(so, null, ref _cachedEditor);
        }

        private void OnGUI()
        {
            ClearPendingFieldFocus();
            _catalogResult ??= _assetCatalog.Refresh(_filterPathTabs.SelectedPath);
            _contextClickGuard.BeginFrame();

            DrawToolbar();
            _filterPathTabBar.Draw(
                _filterPathTabs,
                SelectFilterPathTab,
                _filterPathTabPopupController.ShowAddPopup,
                _filterPathTabPopupController.QueueEditPopup,
                _filterPathTabPopupController.RemoveTab,
                _filterPathTabPopupController.ShowTabContextMenu,
                _contextClickGuard.RegisterBlocker);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            _listPanel.Draw(
                position.width * 0.3f,
                _catalogResult,
                _selectedSettings,
                SelectSO,
                _contextClickGuard.RegisterBlocker);

            ClearPendingFieldFocus();
            _inspectorPanel.Draw(
                _selectedSettings,
                _cachedEditor,
                _contextClickGuard.RegisterBlocker);
            GUILayout.EndHorizontal();

            _filterPathTabPopupController.DrawPendingPopup();
            _contextClickGuard.HandleContextClick(rect =>
                _filterPathTabPopupController.ShowPanelContextMenu(rect, RefreshList, Close));
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search:", GUILayout.Width(50));
            _listPanel.SearchQuery = GUILayout.TextField(
                _listPanel.SearchQuery,
                EditorStyles.toolbarSearchField,
                GUILayout.Width(220));
            _contextClickGuard.RegisterBlocker(GUILayoutUtility.GetLastRect());

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                _listPanel.SearchQuery = "";
            }
            _contextClickGuard.RegisterBlocker(GUILayoutUtility.GetLastRect());

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshList();
            }
            _contextClickGuard.RegisterBlocker(GUILayoutUtility.GetLastRect());

            if (GUILayout.Button("Open Settings Folder", EditorStyles.toolbarButton, GUILayout.Width(130)))
            {
                OpenSettingsFolder();
            }
            _contextClickGuard.RegisterBlocker(GUILayoutUtility.GetLastRect());

            GUILayout.EndHorizontal();
        }

        private void OpenSettingsFolder()
        {
            string stateDirectoryPath = _filterPathTabs.StateDirectoryPath;

            if (!Directory.Exists(stateDirectoryPath))
                Directory.CreateDirectory(stateDirectoryPath);

            EditorUtility.RevealInFinder(stateDirectoryPath);
        }

        private void SelectFilterPathTab(int tabIndex)
        {
            if (!_filterPathTabs.Select(tabIndex))
                return;

            OnFilterPathChanged();
        }

        private void OnFilterPathChanged()
        {
            ClearSelectedSO();
            RefreshList();
        }

        private void ClearSelectedSO()
        {
            _clearFieldFocusPending = true;
            _selectedSettings = null;

            if (_cachedEditor != null)
            {
                DestroyImmediate(_cachedEditor);
                _cachedEditor = null;
            }
        }

        private void ClearPendingFieldFocus()
        {
            if (!_clearFieldFocusPending)
                return;

            // Run in this window's GUI context, including changes made by popup callbacks.
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            _clearFieldFocusPending = false;
        }
    }
}
