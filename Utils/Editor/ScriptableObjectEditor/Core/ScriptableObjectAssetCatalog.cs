using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectAssetCatalog
    {
        public ScriptableObjectAssetCatalogResult Refresh(string filterPath)
        {
            var soList = new List<ScriptableObject>();
            var groupedSOs = new Dictionary<string, List<ScriptableObject>>();

            // Ищем только в выбранной папке: поиск по всему проекту грузил бы каждый SO из пакетов.
            if (string.IsNullOrEmpty(filterPath) || !AssetDatabase.IsValidFolder(filterPath))
                return new ScriptableObjectAssetCatalogResult(soList, groupedSOs);

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { filterPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null)
                    continue;

                soList.Add(so);

                var attr = so.GetType().GetCustomAttribute<SOGroupAttribute>();
                string group = attr?.GroupName ?? "Ungrouped";

                if (!groupedSOs.ContainsKey(group))
                    groupedSOs[group] = new List<ScriptableObject>();

                groupedSOs[group].Add(so);
            }

            foreach (var items in groupedSOs.Values)
            {
                items.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            }

            return new ScriptableObjectAssetCatalogResult(soList, groupedSOs);
        }
    }

    public class ScriptableObjectAssetCatalogResult
    {
        public IReadOnlyList<ScriptableObject> ScriptableObjects { get; }
        public IReadOnlyDictionary<string, List<ScriptableObject>> GroupedScriptableObjects { get; }

        public ScriptableObjectAssetCatalogResult(
            IReadOnlyList<ScriptableObject> scriptableObjects,
            IReadOnlyDictionary<string, List<ScriptableObject>> groupedScriptableObjects)
        {
            ScriptableObjects = scriptableObjects;
            GroupedScriptableObjects = groupedScriptableObjects;
        }
    }
}
