using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Utils.EditorTools
{
    public class LoremIpsum : EditorWindow
    {
        private static readonly string[] Words =
        {
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
            "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore",
            "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud",
            "exercitation", "ullamco", "laboris", "nisi", "aliquip", "ex", "ea", "commodo",
            "consequat", "duis", "aute", "irure", "in", "reprehenderit", "voluptate",
            "velit", "esse", "cillum", "fugiat", "nulla", "pariatur", "excepteur", "sint",
            "occaecat", "cupidatat", "non", "proident", "sunt", "culpa", "qui", "officia",
            "deserunt", "mollit", "anim", "id", "est", "laborum", "at", "vero", "eos",
            "accusamus", "iusto", "odio", "dignissimos", "ducimus", "blanditiis",
            "praesentium", "voluptatum", "deleniti", "atque", "corrupti", "quos", "dolores",
            "quas", "molestias", "recusandae", "itaque", "earum", "rerum", "hic", "tenetur",
            "sapiente", "delectus", "aut", "reiciendis", "voluptatibus", "maiores", "alias",
            "consequatur", "perferendis", "doloribus", "asperiores", "repellat"
        };

        private int wordCount = 10;
        private bool includeChildren = true;
        private Vector2 scrollPos;

        // Превью строится от seed, а не от нового Random на каждый OnGUI: иначе текст
        // менялся бы при каждой перерисовке окна.
        private int seed = System.Environment.TickCount;

        [MenuItem("Tools/Lorem Ipsum Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<LoremIpsum>("Lorem Ipsum");
            window.minSize = new Vector2(300, 200);
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Lorem Ipsum Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            wordCount = EditorGUILayout.IntField("Word Count", wordCount);
            wordCount = Mathf.Max(1, wordCount);

            includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);

            EditorGUILayout.Space(8);

            // Preview
            EditorGUILayout.LabelField("Preview:", EditorStyles.miniLabel);
            string preview = Generate(wordCount, new System.Random(seed));
            EditorGUILayout.SelectableLabel(preview, EditorStyles.wordWrappedLabel,
                GUILayout.MinHeight(40));

            if (GUILayout.Button("Shuffle"))
                seed = System.Environment.TickCount ^ seed * 397;

            EditorGUILayout.Space(8);

            // Show selected objects and their text components
            var selected = Selection.gameObjects;

            if (selected == null || selected.Length == 0)
            {
                EditorGUILayout.HelpBox("Select GameObjects in the Hierarchy to apply Lorem Ipsum to their text components.", MessageType.Info);
                return;
            }

            // Gather all text components
            var targets = new List<(GameObject go, Component comp)>();

            foreach (var go in selected)
            {
                CollectTextComponents(go, targets, includeChildren);
            }

            EditorGUILayout.LabelField($"Found {targets.Count} text component(s):", EditorStyles.miniLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (var (go, comp) in targets)
            {
                string type = comp is TMP_Text ? "TMP" : "Text";
                EditorGUILayout.LabelField($"  {go.name}  [{type}]", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);

            GUI.enabled = targets.Count > 0;

            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                // Первый компонент получает текст из превью, остальные — свой: одинаковые
                // подписи на всех кнопках выглядели бы неправдоподобно.
                var random = new System.Random(seed);
                Undo.RecordObjects(GetComponents(targets), "Lorem Ipsum Generate");

                foreach (var (go, comp) in targets)
                {
                    string text = Generate(wordCount, random);

                    if (comp is TMP_Text tmp)
                    {
                        tmp.text = text;
                        EditorUtility.SetDirty(tmp);
                    }
                    else if (comp is Text legacy)
                    {
                        legacy.text = text;
                        EditorUtility.SetDirty(legacy);
                    }
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Height(24)))
            {
                Undo.RecordObjects(GetComponents(targets), "Lorem Ipsum Clear");

                foreach (var (go, comp) in targets)
                {
                    if (comp is TMP_Text tmp)
                    {
                        tmp.text = string.Empty;
                        EditorUtility.SetDirty(tmp);
                    }
                    else if (comp is Text legacy)
                    {
                        legacy.text = string.Empty;
                        EditorUtility.SetDirty(legacy);
                    }
                }
            }

            GUI.enabled = true;
        }

        private static void CollectTextComponents(GameObject go, List<(GameObject, Component)> results, bool children)
        {
            if (children)
            {
                foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    results.Add((tmp.gameObject, tmp));
                }

                foreach (var text in go.GetComponentsInChildren<Text>(true))
                {
                    results.Add((text.gameObject, text));
                }
            }
            else
            {
                var tmp = go.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    results.Add((go, tmp));
                }

                var text = go.GetComponent<Text>();
                if (text != null)
                {
                    results.Add((go, text));
                }
            }
        }

        private static Object[] GetComponents(List<(GameObject go, Component comp)> targets)
        {
            var arr = new Object[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                arr[i] = targets[i].comp;
            }
            return arr;
        }

        /// <summary>Случайный текст из count слов: первое с заглавной, точка в конце.</summary>
        public static string Generate(int count) => Generate(count, new System.Random());

        public static string Generate(int count, System.Random random)
        {
            if (count <= 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            int previous = -1;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    sb.Append(' ');

                // Одно слово два раза подряд сразу выдаёт генератор.
                int index = random.Next(Words.Length);
                if (index == previous)
                    index = (index + 1) % Words.Length;
                previous = index;

                string word = Words[index];

                if (i == 0)
                    word = char.ToUpper(word[0]) + word.Substring(1);

                sb.Append(word);
            }

            sb.Append('.');
            return sb.ToString();
        }
    }
}
