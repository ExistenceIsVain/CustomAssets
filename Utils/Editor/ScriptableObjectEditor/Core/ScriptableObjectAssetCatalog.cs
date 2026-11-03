using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectAssetCatalog
    {
        public const string UngroupedName = "Ungrouped";

        // Предупреждение о переопределённой группе — один раз на тип за сессию редактора.
        private static readonly HashSet<Type> WarnedGroupOverrides = new();

        public ScriptableObjectAssetCatalogResult Refresh(string filterPath)
        {
            var soList = new List<ScriptableObject>();
            var groups = new Dictionary<string, ScriptableObjectGroup>();

            // Ищем только в выбранной папке: поиск по всему проекту грузил бы каждый SO из пакетов.
            if (string.IsNullOrEmpty(filterPath) || !AssetDatabase.IsValidFolder(filterPath))
                return new ScriptableObjectAssetCatalogResult(soList, new List<ScriptableObjectGroup>());

            var placements = new Dictionary<Type, (string group, string subGroup)>();

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { filterPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null)
                    continue;

                soList.Add(so);

                Type type = so.GetType();
                if (!placements.TryGetValue(type, out var placement))
                {
                    placement = (ResolveGroup(type), ResolveSubGroup(type));
                    placements[type] = placement;
                }

                if (!groups.TryGetValue(placement.group, out var group))
                {
                    group = new ScriptableObjectGroup(placement.group);
                    groups[placement.group] = group;
                }

                group.Add(so, placement.subGroup);
            }

            foreach (var group in groups.Values)
                group.Sort();

            // По алфавиту, Ungrouped — последней.
            var ordered = groups.Values
                .OrderBy(g => g.Name == UngroupedName)
                .ThenBy(g => g.Name, StringComparer.Ordinal)
                .ToList();

            return new ScriptableObjectAssetCatalogResult(soList, ordered);
        }

        /// <summary>
        /// Группа — от самого базового класса с [SOGroup]. Наследник не может её сменить:
        /// другое имя на наследнике игнорируется с предупреждением.
        /// </summary>
        private static string ResolveGroup(Type type)
        {
            string group = null;
            Type groupOwner = null;
            var overrides = new List<(Type type, string name)>();

            for (Type t = type; t != null && t != typeof(ScriptableObject); t = t.BaseType)
            {
                var attr = t.GetCustomAttribute<SOGroupAttribute>(inherit: false);
                if (attr == null || string.IsNullOrWhiteSpace(attr.GroupName))
                    continue;

                if (group != null)
                    overrides.Add((groupOwner, group));

                group = attr.GroupName;
                groupOwner = t;
            }

            foreach (var (overrideType, overrideName) in overrides)
            {
                if (overrideName == group || !WarnedGroupOverrides.Add(overrideType))
                    continue;

                Debug.LogWarning(
                    $"[SO Editor] {overrideType.Name}: [SOGroup(\"{overrideName}\")] игнорируется — группу задаёт " +
                    $"базовый класс {groupOwner.Name} (\"{group}\"). Наследник может менять только [SOSubGroup].");
            }

            return group ?? UngroupedName;
        }

        /// <summary>Сабгруппа — от ближайшего класса с [SOSubGroup]: наследник может её переопределить.</summary>
        private static string ResolveSubGroup(Type type)
        {
            var attr = type.GetCustomAttribute<SOSubGroupAttribute>(inherit: true);
            return attr == null || string.IsNullOrWhiteSpace(attr.SubGroupName) ? null : attr.SubGroupName;
        }
    }

    /// <summary>Группа окна: ассеты без сабгруппы и сабгруппы со своими ассетами.</summary>
    public class ScriptableObjectGroup
    {
        private readonly SortedDictionary<string, List<ScriptableObject>> _subGroups = new(StringComparer.Ordinal);
        private readonly List<ScriptableObject> _items = new();

        public ScriptableObjectGroup(string name)
        {
            Name = name;
        }

        public string Name { get; }

        /// <summary>Ассеты без сабгруппы.</summary>
        public IReadOnlyList<ScriptableObject> Items => _items;

        public IReadOnlyDictionary<string, List<ScriptableObject>> SubGroups => _subGroups;

        public void Add(ScriptableObject so, string subGroup)
        {
            if (subGroup == null)
            {
                _items.Add(so);
                return;
            }

            if (!_subGroups.TryGetValue(subGroup, out var items))
            {
                items = new List<ScriptableObject>();
                _subGroups[subGroup] = items;
            }

            items.Add(so);
        }

        public void Sort()
        {
            _items.Sort(CompareByName);

            foreach (var items in _subGroups.Values)
                items.Sort(CompareByName);
        }

        private static int CompareByName(ScriptableObject a, ScriptableObject b) =>
            string.Compare(a.name, b.name, StringComparison.Ordinal);
    }

    public class ScriptableObjectAssetCatalogResult
    {
        public IReadOnlyList<ScriptableObject> ScriptableObjects { get; }

        /// <summary>Группы в порядке показа.</summary>
        public IReadOnlyList<ScriptableObjectGroup> Groups { get; }

        public ScriptableObjectAssetCatalogResult(
            IReadOnlyList<ScriptableObject> scriptableObjects,
            IReadOnlyList<ScriptableObjectGroup> groups)
        {
            ScriptableObjects = scriptableObjects;
            Groups = groups;
        }
    }
}
