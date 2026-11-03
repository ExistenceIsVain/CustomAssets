using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectFilterPathTabs
    {
        public const string DefaultFilterPath = "Assets";

        // UserSettings/ — личные настройки редактора в папке проекта: не попадают в git
        // (стандартный Unity .gitignore) и не смешиваются с сейвами игры в persistentDataPath.
        private const string TabsStateDirectoryName = "UserSettings/ScriptableObjectEditor";
        private const string TabsStateFileName = "filter-path-tabs.json";

        private readonly List<FilterPathTab> _tabs = new();
        private int _selectedIndex;

        public IReadOnlyList<FilterPathTab> Tabs => _tabs;
        public int SelectedIndex => _selectedIndex;
        public string StateDirectoryPath => GetTabsStateDirectoryPath();
        public FilterPathTab SelectedTab
        {
            get
            {
                EnsureTabs();
                return _tabs[_selectedIndex];
            }
        }

        public string SelectedPath => SelectedTab.Path;

        public void Load()
        {
            _tabs.Clear();
            _selectedIndex = 0;

            string statePath = GetTabsStateFilePath();

            try
            {
                if (File.Exists(statePath))
                {
                    string json = File.ReadAllText(statePath);
                    var state = JsonUtility.FromJson<FilterPathTabsState>(json);

                    if (state?.tabs != null)
                    {
                        foreach (var tab in state.tabs)
                        {
                            string name = tab?.Name?.Trim();
                            string path = NormalizePath(tab?.Path);

                            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
                                continue;

                            _tabs.Add(new FilterPathTab(name, path));
                        }

                        _selectedIndex = state.selectedIndex;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SO Editor] Failed to load filter path tabs: {exception.Message}");
            }

            EnsureTabs();
        }

        public void Save()
        {
            EnsureTabs();

            string statePath = GetTabsStateFilePath();
            string stateDirectory = Path.GetDirectoryName(statePath);

            try
            {
                if (!Directory.Exists(stateDirectory))
                    Directory.CreateDirectory(stateDirectory);

                var state = new FilterPathTabsState
                {
                    tabs = _tabs,
                    selectedIndex = _selectedIndex
                };

                File.WriteAllText(statePath, JsonUtility.ToJson(state, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SO Editor] Failed to save filter path tabs: {exception.Message}");
            }
        }

        public bool Select(int index)
        {
            if (index < 0 || index >= _tabs.Count || index == _selectedIndex)
                return false;

            _selectedIndex = index;
            Save();
            return true;
        }

        public bool Add(string name, string path)
        {
            if (!TryNormalizeTab(name, path, out string normalizedName, out string normalizedPath))
                return false;

            _tabs.Add(new FilterPathTab(normalizedName, normalizedPath));
            _selectedIndex = _tabs.Count - 1;
            Save();
            return true;
        }

        public bool Edit(int index, string name, string path)
        {
            if (index < 0 || index >= _tabs.Count)
                return false;

            if (!TryNormalizeTab(name, path, out string normalizedName, out string normalizedPath))
                return false;

            _tabs[index].Set(normalizedName, normalizedPath);
            Save();
            return true;
        }

        public bool Remove(int index)
        {
            if (_tabs.Count <= 1 || index < 0 || index >= _tabs.Count)
                return false;

            _tabs.RemoveAt(index);

            if (_selectedIndex > index)
                _selectedIndex--;
            else if (_selectedIndex >= _tabs.Count)
                _selectedIndex = _tabs.Count - 1;

            EnsureTabs();
            Save();
            return true;
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            return path.Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static string GetTabsStateFilePath()
        {
            return Path.Combine(GetTabsStateDirectoryPath(), TabsStateFileName);
        }

        private static string GetTabsStateDirectoryPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, TabsStateDirectoryName);
        }

        private static bool TryNormalizeTab(string name, string path, out string normalizedName, out string normalizedPath)
        {
            normalizedName = name?.Trim();
            normalizedPath = NormalizePath(path);

            return !string.IsNullOrEmpty(normalizedName) && !string.IsNullOrEmpty(normalizedPath);
        }

        private void EnsureTabs()
        {
            if (_tabs.Count == 0)
            {
                _tabs.Add(new FilterPathTab("Default", DefaultFilterPath));
                _selectedIndex = 0;
                return;
            }

            bool selectedIndexIsValid = _selectedIndex >= 0 && _selectedIndex < _tabs.Count;

            if (selectedIndexIsValid)
                return;

            int defaultIndex = _tabs.FindIndex(tab => NormalizePath(tab.Path) == DefaultFilterPath);

            if (defaultIndex < 0)
            {
                _tabs.Insert(0, new FilterPathTab("Default", DefaultFilterPath));
                defaultIndex = 0;
            }

            _selectedIndex = defaultIndex;
        }

        [Serializable]
        public class FilterPathTab
        {
            [SerializeField] private string name;
            [SerializeField] private string path;

            public string Name => name;
            public string Path => path;

            public FilterPathTab()
            {
            }

            public FilterPathTab(string name, string path)
            {
                Set(name, path);
            }

            internal void Set(string name, string path)
            {
                this.name = name;
                this.path = NormalizePath(path);
            }
        }

        [Serializable]
        private class FilterPathTabsState
        {
            public List<FilterPathTab> tabs = new();
            public int selectedIndex;
        }
    }
}
