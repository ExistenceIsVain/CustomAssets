using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    /// <summary>
    /// Инспектор [SerializeReference]-списка абстрактных классов:
    /// сворачиваемый список с прокруткой, Add с выбором наследника, Expand/Collapse All,
    /// у элементов — перемещение вверх/вниз, копирование, вставка и удаление.
    /// </summary>
    public static class SerializeReferenceListGUI
    {
        // Одна копия на весь редактор. SessionState живёт до закрытия Unity и переживает
        // перезагрузку домена при входе в Play Mode; системный буфер обмена не трогается.
        private const string ClipboardTypeKey = "Utils.SerializeReferenceListGUI.ClipboardType";
        private const string ClipboardJsonKey = "Utils.SerializeReferenceListGUI.ClipboardJson";

        private const float IconButtonWidth = 22f;

        private static readonly Dictionary<string, Vector2> scrollPositions = new Dictionary<string, Vector2>();

        private enum ElementAction
        {
            None,
            MoveUp,
            MoveDown,
            Paste,
            Delete
        }

        /// <param name="list">Свойство [SerializeReference]-списка.</param>
        /// <param name="baseType">Базовый тип элементов: в меню Add попадают его неабстрактные наследники.</param>
        /// <param name="title">Заголовок списка. По нему список сворачивается.</param>
        /// <param name="readableName">Имя типа для меню и заголовков. По умолчанию — разбитое по словам имя класса.</param>
        /// <param name="headerButtons">Доп. кнопки в заголовке элемента, до стандартных. Аргументы: элемент и его значение.</param>
        /// <param name="elementLabel">Доп. подпись в заголовке элемента после имени типа, например его тег. Пусто — без подписи.</param>
        /// <param name="maxHeight">Высота, после которой список прокручивается.</param>
        /// <param name="elementEnabled">Включён ли элемент: кружок после названия, зелёный — включён, красный — выключен. Null — без кружка.</param>
        /// <param name="addButtonLabel">Подпись кнопки добавления элемента.</param>
        /// <param name="elementVisible">Показывать ли элемент: скрытые не рисуются, стрелки переставляют среди видимых. Null — показываются все.</param>
        public static void Draw(
            SerializedProperty list,
            Type baseType,
            string title,
            Func<Type, string> readableName = null,
            Action<SerializedProperty, object> headerButtons = null,
            Func<object, string> elementLabel = null,
            float maxHeight = 450f,
            Func<object, bool> elementEnabled = null,
            Func<object, bool> elementVisible = null,
            string addButtonLabel = "Add")
        {
            readableName ??= DefaultName;

            CollectVisible(list, elementVisible);

            string count = visibleIndices.Count == list.arraySize
                ? list.arraySize.ToString()
                : $"{visibleIndices.Count}/{list.arraySize}";

            list.isExpanded = EditorGUILayout.Foldout(list.isExpanded, $"{title} ({count})", true, EditorStyles.foldoutHeader);

            if (!list.isExpanded)
                return;

            DrawToolbar(list, baseType, readableName, addButtonLabel);
            EditorGUILayout.Space(2);

            string scrollKey = list.serializedObject.targetObject.GetInstanceID() + "/" + list.propertyPath;
            scrollPositions.TryGetValue(scrollKey, out Vector2 scroll);
            contentHeights.TryGetValue(scrollKey, out float contentHeight);

            // Scroll view в раскладке растягивается на всё свободное место, даже с MaxHeight,
            // и короткий список уезжал бы вниз инспектора. Поэтому прокрутка включается, только
            // когда содержимое по замеру прошлой отрисовки выше maxHeight, и с точной высотой.
            bool scrolls = contentHeight > maxHeight;

            if (scrolls)
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(maxHeight));

            EditorGUILayout.BeginVertical();

            ElementAction action = ElementAction.None;
            int actionIndex = -1;
            int targetIndex = -1;

            for (int p = 0; p < visibleIndices.Count; p++)
            {
                int i = visibleIndices[p];
                int previous = p > 0 ? visibleIndices[p - 1] : -1;
                int next = p < visibleIndices.Count - 1 ? visibleIndices[p + 1] : -1;

                ElementAction elementAction = DrawElement(
                    list, i, previous >= 0, next >= 0, baseType, readableName, headerButtons, elementLabel, elementEnabled);

                if (elementAction != ElementAction.None && action == ElementAction.None)
                {
                    action = elementAction;
                    actionIndex = i;
                    targetIndex = elementAction == ElementAction.MoveUp ? previous : next;
                }
            }

            EditorGUILayout.EndVertical();
            MeasureContent(scrollKey, contentHeight);

            if (scrolls)
            {
                EditorGUILayout.EndScrollView();
                scrollPositions[scrollKey] = scroll;
            }

            // Массив меняется только после обхода, иначе индексы и раскладка GUI разъедутся посреди кадра.
            Apply(list, baseType, action, actionIndex, targetIndex);
        }

        // Высота элементов списка с прошлой отрисовки — по ней решается, нужна ли прокрутка.
        private static readonly Dictionary<string, float> contentHeights = new Dictionary<string, float>();

        /// <summary>
        /// Запоминает высоту только что закрытой группы элементов. Размеры известны лишь на Repaint;
        /// если высота изменилась, инспектор перерисовывается, чтобы прокрутка включилась или
        /// выключилась сразу, а не при следующем движении мыши.
        /// </summary>
        private static void MeasureContent(string key, float previousHeight)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            float height = GUILayoutUtility.GetLastRect().height;

            if (Mathf.Approximately(height, previousHeight))
                return;

            contentHeights[key] = height;
            // Перерисовать окно, в котором сейчас рисуется список (инспектор или своё окно).
            HandleUtility.Repaint();
        }

        // Индексы видимых элементов текущего списка. Заполняется в начале Draw и живёт один вызов.
        private static readonly List<int> visibleIndices = new List<int>();

        private static void CollectVisible(SerializedProperty list, Func<object, bool> elementVisible)
        {
            visibleIndices.Clear();

            for (int i = 0; i < list.arraySize; i++)
            {
                object value = list.GetArrayElementAtIndex(i).managedReferenceValue;

                // Пустые ссылки показываем всегда: DrawElement их вычищает.
                if (value == null || elementVisible == null || elementVisible(value))
                    visibleIndices.Add(i);
            }
        }

        /// <summary>Разбивает CamelCase по словам и убирает суффикс Drawer.</summary>
        public static string DefaultName(Type type)
        {
            string name = type.Name;

            if (name.EndsWith("Drawer", StringComparison.OrdinalIgnoreCase) && name.Length > 6)
                name = name.Substring(0, name.Length - 6);

            return SplitWords(name);
        }

        public static string SplitWords(string name)
        {
            return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        // ---------------------------------------------------------------- разметка

        private static void DrawToolbar(SerializedProperty list, Type baseType, Func<Type, string> readableName, string addButtonLabel)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(addButtonLabel, GUILayout.Width(110)))
                ShowAddMenu(list, baseType, readableName);

            using (new EditorGUI.DisabledScope(!CanPaste(baseType)))
            {
                if (GUILayout.Button(new GUIContent("Paste", PasteTooltip() + " Вставится в конец списка."), GUILayout.Width(60)))
                {
                    Paste(list, baseType, list.arraySize);
                    list.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            GUILayout.FlexibleSpace();

            if (visibleIndices.Count > 0)
            {
                if (GUILayout.Button("Expand All", GUILayout.Width(80)))
                    SetAllExpanded(list, true);

                if (GUILayout.Button("Collapse All", GUILayout.Width(85)))
                    SetAllExpanded(list, false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static ElementAction DrawElement(
            SerializedProperty list,
            int index,
            bool canMoveUp,
            bool canMoveDown,
            Type baseType,
            Func<Type, string> readableName,
            Action<SerializedProperty, object> headerButtons,
            Func<object, string> elementLabel,
            Func<object, bool> elementEnabled)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            object value = element.managedReferenceValue;

            // Тип элемента удалили или переименовали — ссылка пустая, чинить нечего.
            if (value == null)
                return ElementAction.Delete;

            ElementAction action = ElementAction.None;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(15);

            string label = elementLabel?.Invoke(value);
            string header = string.IsNullOrEmpty(label)
                ? $"{readableName(value.GetType())} #{index + 1}"
                : $"{readableName(value.GetType())} {label} #{index + 1}";

            if (elementEnabled != null)
                header += elementEnabled(value) ? EnabledDot : DisabledDot;

            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, header, true, RichFoldout);

            GUILayout.FlexibleSpace();

            headerButtons?.Invoke(element, value);

            using (new EditorGUI.DisabledScope(!canMoveUp))
            {
                if (GUILayout.Button(new GUIContent("▲", "Переместить вверх"), GUILayout.Width(IconButtonWidth)))
                    action = ElementAction.MoveUp;
            }

            using (new EditorGUI.DisabledScope(!canMoveDown))
            {
                if (GUILayout.Button(new GUIContent("▼", "Переместить вниз"), GUILayout.Width(IconButtonWidth)))
                    action = ElementAction.MoveDown;
            }

            if (GUILayout.Button(new GUIContent("Copy", "Запомнить этот элемент. Хранится одна копия."), GUILayout.Width(44)))
                Copy(value);

            using (new EditorGUI.DisabledScope(!CanPaste(baseType)))
            {
                if (GUILayout.Button(new GUIContent("Paste", PasteTooltip() + " Вставится под этим элементом."), GUILayout.Width(48)))
                    action = ElementAction.Paste;
            }

            if (GUILayout.Button(new GUIContent("X", "Удалить"), GUILayout.Width(IconButtonWidth)))
                action = ElementAction.Delete;

            EditorGUILayout.EndHorizontal();

            if (element.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawChildProperties(element);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            return action;
        }

        // Кружок состояния — символ в конце заголовка, покрашенный rich text.
        private const string EnabledDot = " <color=#4CD959>●</color>";
        private const string DisabledDot = " <color=#E64040>●</color>";

        private static GUIStyle richFoldout;

        private static GUIStyle RichFoldout => richFoldout ??= new GUIStyle(EditorStyles.foldout) { richText = true };

        private static void DrawChildProperties(SerializedProperty parent)
        {
            SerializedProperty iterator = parent.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            if (!iterator.NextVisible(true))
                return;

            while (!SerializedProperty.EqualContents(iterator, end))
            {
                EditorGUILayout.PropertyField(iterator, new GUIContent(iterator.displayName), true);

                if (!iterator.NextVisible(false))
                    break;
            }
        }

        // ---------------------------------------------------------------- действия

        /// <param name="target">Для перемещения — индекс соседнего видимого элемента, на место которого встаёт элемент.</param>
        private static void Apply(SerializedProperty list, Type baseType, ElementAction action, int index, int target)
        {
            switch (action)
            {
                case ElementAction.MoveUp:
                case ElementAction.MoveDown:
                    Move(list, index, target);
                    break;

                case ElementAction.Paste:
                    Paste(list, baseType, index + 1);
                    break;

                case ElementAction.Delete:
                    list.DeleteArrayElementAtIndex(index);
                    break;

                default:
                    return;
            }

            // Раскладка этого кадра уже не совпадает с массивом: прерываем отрисовку.
            list.serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        private static readonly List<bool> expandedBuffer = new List<bool>();

        private static void Move(SerializedProperty list, int from, int to)
        {
            if (to < 0 || to >= list.arraySize || from == to)
                return;

            // isExpanded у элемента хранится по пути, а путь содержит индекс. MoveArrayElement сдвигает
            // все элементы между from и to (между ними могут быть скрытые), поэтому состояния
            // переставляем тем же сдвигом.
            expandedBuffer.Clear();

            for (int i = 0; i < list.arraySize; i++)
                expandedBuffer.Add(list.GetArrayElementAtIndex(i).isExpanded);

            bool moved = expandedBuffer[from];
            expandedBuffer.RemoveAt(from);
            expandedBuffer.Insert(to, moved);

            list.MoveArrayElement(from, to);

            for (int i = 0; i < list.arraySize; i++)
                list.GetArrayElementAtIndex(i).isExpanded = expandedBuffer[i];
        }

        /// <summary>Разворачивает или сворачивает только видимые элементы.</summary>
        private static void SetAllExpanded(SerializedProperty list, bool expanded)
        {
            for (int p = 0; p < visibleIndices.Count; p++)
                list.GetArrayElementAtIndex(visibleIndices[p]).isExpanded = expanded;
        }

        // ---------------------------------------------------------------- копирование

        /// <summary>
        /// Сериализует элемент через EditorJsonUtility: ссылки на ассеты (материалы, префабы)
        /// сохраняются по GUID и восстанавливаются при вставке, в том числе после Play Mode.
        /// </summary>
        private static void Copy(object value)
        {
            SessionState.SetString(ClipboardTypeKey, value.GetType().AssemblyQualifiedName);
            SessionState.SetString(ClipboardJsonKey, EditorJsonUtility.ToJson(value));
        }

        private static Type ClipboardType()
        {
            string typeName = SessionState.GetString(ClipboardTypeKey, string.Empty);

            return string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName, false);
        }

        private static bool CanPaste(Type baseType)
        {
            Type type = ClipboardType();

            return type != null && !type.IsAbstract && baseType.IsAssignableFrom(type);
        }

        private static string PasteTooltip()
        {
            Type type = ClipboardType();

            return type != null ? $"Вставить копию: {type.Name}." : "Буфер пуст: сначала нажмите Copy у элемента.";
        }

        private static void Paste(SerializedProperty list, Type baseType, int index)
        {
            if (!CanPaste(baseType))
                return;

            object value = Activator.CreateInstance(ClipboardType());
            EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(ClipboardJsonKey, "{}"), value);

            index = Mathf.Clamp(index, 0, list.arraySize);

            list.InsertArrayElementAtIndex(index);

            SerializedProperty element = list.GetArrayElementAtIndex(index);
            element.managedReferenceValue = value;
            element.isExpanded = true;
        }

        // ---------------------------------------------------------------- добавление

        private static void ShowAddMenu(SerializedProperty list, Type baseType, Func<Type, string> readableName)
        {
            // Меню срабатывает после OnInspectorGUI, когда свойство уже недействительно:
            // запоминаем объект и путь и находим свойство заново.
            SerializedObject serializedObject = list.serializedObject;
            string path = list.propertyPath;

            GenericMenu menu = new GenericMenu();

            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType)
                         .Where(t => !t.IsAbstract && !t.IsGenericType)
                         .OrderBy(readableName))
            {
                menu.AddItem(new GUIContent(readableName(type)), false, () => Add(serializedObject, path, type));
            }

            menu.ShowAsContext();
        }

        private static void Add(SerializedObject serializedObject, string path, Type type)
        {
            serializedObject.Update();

            SerializedProperty list = serializedObject.FindProperty(path);
            int index = list.arraySize;

            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
