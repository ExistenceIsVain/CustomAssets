#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Utils.Messaging;

namespace Utils.EditorTools
{
    public class TransmitterMonitorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool showReceivers = true;
        private bool showProviders = true;
        private bool showDeadReferences = true;
        private bool autoRefresh = true;
        private float refreshInterval = 1f;
        private double lastRefreshTime;

        private bool receiversExpanded = true;
        private bool providersExpanded = true;

        private Dictionary<string, bool> foldoutStates = new();

        private GUIStyle? headerStyle;
        private GUIStyle? deadReferenceStyle;
        private GUIStyle? aliveReferenceStyle;

        [MenuItem("Tools/Transmitter Monitor")]
        public static void ShowWindow()
        {
            GetWindow<TransmitterMonitorWindow>("Transmitter Monitor");
        }

        private void OnEnable()
        {
            Transmitter.OnRegistryChanged += Repaint;
            lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            Transmitter.OnRegistryChanged -= Repaint;
        }

        private void InitializeStyles()
        {
            headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            deadReferenceStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.red }
            };

            aliveReferenceStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.green }
            };
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
            {
                lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }

            DrawTransmitterHeader();
            DrawControls();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (showReceivers) DrawReceivers();
            if (showProviders) DrawProviders();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTransmitterHeader()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Transmitter Monitor", headerStyle);
            EditorGUILayout.Space();
        }

        private void DrawControls()
        {
            // Toggles
            EditorGUILayout.BeginHorizontal();
            showReceivers = EditorGUILayout.Toggle("Show Receivers", showReceivers);
            showProviders = EditorGUILayout.Toggle("Show Providers", showProviders);
            showDeadReferences = EditorGUILayout.Toggle("Show Dead References", showDeadReferences);
            EditorGUILayout.EndHorizontal();

            // Auto refresh
            EditorGUILayout.BeginHorizontal();
            autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
            if (autoRefresh)
                refreshInterval = EditorGUILayout.Slider("Refresh Rate (s)", refreshInterval, 0.1f, 5f);
            if (GUILayout.Button("Manual Refresh"))
                Repaint();
            EditorGUILayout.EndHorizontal();

            // Collapse/Expand
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Collapse All Receivers")) CollapseAllInCategory("Receivers");
            if (GUILayout.Button("Expand All Receivers")) ExpandAllInCategory("Receivers");
            if (GUILayout.Button("Collapse All Providers")) CollapseAllInCategory("Providers");
            if (GUILayout.Button("Expand All Providers")) ExpandAllInCategory("Providers");
            EditorGUILayout.EndHorizontal();

            // Actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cleanup Dead References"))
                Transmitter.CleanupDeadReferences();
            if (GUILayout.Button("Clear All"))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Transmitter Entries",
                    "This will clear all registered receivers and providers. Are you sure?",
                    "Yes", "No"))
                {
                    Transmitter.ClearAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private void DrawReceivers()
        {
            var entries = Transmitter.GetReceiverEntries();

            EditorGUILayout.BeginHorizontal();
            receiversExpanded = EditorGUILayout.Foldout(receiversExpanded, $"RECEIVERS ({GetTotalCount(entries)})", headerStyle);
            EditorGUILayout.EndHorizontal();

            if (!receiversExpanded) return;

            EditorGUILayout.Space();

            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("No receivers registered", EditorStyles.helpBox);
                return;
            }

            foreach (var kvp in entries.OrderBy(x => x.Key.Name))
                DrawTypeSection($"Receivers<{kvp.Key.Name}>", kvp.Value);

            EditorGUILayout.Space();
        }

        private void DrawProviders()
        {
            var entries = Transmitter.GetProviderEntries();

            EditorGUILayout.BeginHorizontal();
            providersExpanded = EditorGUILayout.Foldout(providersExpanded, $"PROVIDERS ({GetTotalCount(entries)})", headerStyle);
            EditorGUILayout.EndHorizontal();

            if (!providersExpanded) return;

            EditorGUILayout.Space();

            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("No providers registered", EditorStyles.helpBox);
                return;
            }

            foreach (var kvp in entries.OrderBy(x => ProviderSlotName(x.Key)))
                DrawTypeSection($"Providers<{ProviderSlotName(kvp.Key)}>", kvp.Value);
        }

        private void DrawTypeSection(string title, List<TransmitterEntry> entries)
        {
            var aliveCount = entries.Count(e => e.IsAlive());
            var deadCount = entries.Count - aliveCount;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!foldoutStates.ContainsKey(title))
                foldoutStates[title] = true;

            foldoutStates[title] = EditorGUILayout.Foldout(
                foldoutStates[title],
                $"{title} (Alive: {aliveCount}, Dead: {deadCount})",
                EditorStyles.foldoutHeader);

            if (foldoutStates[title])
            {
                foreach (var entry in entries.OrderBy(e => e.isDestroyed).ThenBy(e => e.className))
                {
                    if (!showDeadReferences && entry.isDestroyed)
                        continue;

                    DrawEntry(entry);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawEntry(TransmitterEntry entry)
        {
            var style = entry.IsAlive() ? aliveReferenceStyle : deadReferenceStyle;
            var statusIcon = entry.IsAlive() ? "✓" : "✗";
            var destroyInfo = entry.isDestroyed && entry.destroyTime.HasValue
                ? $" (Destroyed: {entry.destroyTime.Value:HH:mm:ss})"
                : "";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{statusIcon}", style, GUILayout.Width(20));

            // Clickable class name — open the script file
            var script = FindScriptAsset(entry.classType);
            if (script != null)
            {
                if (GUILayout.Button(entry.className, EditorStyles.linkLabel, GUILayout.Width(150)))
                    AssetDatabase.OpenAsset(script);
            }
            else
            {
                EditorGUILayout.LabelField(entry.className, style, GUILayout.Width(150));
            }

            // Clickable object name — select the GameObject in hierarchy
            if (entry.IsAlive() && entry.gameObjectInstanceId.HasValue)
            {
                #if UNITY_6000_3_OR_NEWER
                var go = EditorUtility.EntityIdToObject(entry.gameObjectInstanceId.Value) as GameObject;
#else
                var go = EditorUtility.InstanceIDToObject(entry.gameObjectInstanceId.Value) as GameObject;
#endif
                if (go != null)
                {
                    if (GUILayout.Button(entry.objectName, EditorStyles.linkLabel, GUILayout.Width(200)))
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(entry.objectName, style, GUILayout.Width(200));
                }
            }
            else
            {
                EditorGUILayout.LabelField(entry.objectName, style, GUILayout.Width(200));
            }

            EditorGUILayout.LabelField($"Registered: {entry.registrationTime:HH:mm:ss}{destroyInfo}", style);
            EditorGUILayout.EndHorizontal();
        }

        private static readonly Dictionary<Type, MonoScript?> scriptCache = new();

        private MonoScript? FindScriptAsset(Type type)
        {
            if (scriptCache.TryGetValue(type, out var cached))
                return cached;

            // Fast path: works for MonoBehaviour and ScriptableObject types
            var scripts = MonoImporter.GetAllRuntimeMonoScripts();
            foreach (var script in scripts)
            {
                if (script.GetClass() == type)
                {
                    scriptCache[type] = script;
                    return script;
                }
            }

            // Fallback: search by filename for plain C# classes
            var guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (asset != null && asset.GetClass() == type)
                {
                    scriptCache[type] = asset;
                    return asset;
                }
            }

            scriptCache[type] = null;
            return null;
        }

        // Provider slots are keyed by IProvider<TRequest, TResponse>: show "Request -> Response".
        private static string ProviderSlotName(Type slot)
        {
            if (!slot.IsGenericType)
                return slot.Name;

            Type[] args = slot.GetGenericArguments();
            return args.Length == 2 ? $"{args[0].Name} -> {args[1].Name}" : slot.Name;
        }

        private int GetTotalCount(Dictionary<Type, List<TransmitterEntry>> entries)
        {
            return entries.Values.Sum(list => list.Count);
        }

        private void CollapseAllInCategory(string category)
        {
            foreach (var key in foldoutStates.Keys.Where(k => k.StartsWith(category)).ToList())
                foldoutStates[key] = false;
        }

        private void ExpandAllInCategory(string category)
        {
            foreach (var key in foldoutStates.Keys.Where(k => k.StartsWith(category)).ToList())
                foldoutStates[key] = true;
        }
    }
}
