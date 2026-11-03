using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Utils.EditorTools
{
    /// <summary>
    /// Type dropdown for [SerializeReference] fields. On an array or List&lt;T&gt; field Unity draws each element with
    /// this drawer, so every element gets its own dropdown; elements that share one instance (after "+" or
    /// "Duplicate Array Element") get their own copy.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassDrawerAttribute))]
    public sealed class SubclassDrawer : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;
        private const string ArrayDataMarker = ".Array.data[";

        private static readonly Dictionary<Type, Type[]> ConcreteTypesCache = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(
                    position,
                    $"{label.text} must be marked with [SerializeReference].",
                    MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            Type baseType = GetBaseType(property);
            if (baseType == null)
            {
                EditorGUI.HelpBox(position, $"Cannot determine the base type of {label.text}.", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            if (Event.current.type == EventType.Repaint && SharesReferenceWithEarlierElement(property))
            {
                DetachSharedReference(property);
            }

            Type currentType = property.managedReferenceValue?.GetType();
            DrawHeader(position, property, label, baseType, currentType);

            if (currentType != null && property.isExpanded)
            {
                DrawChildren(position, property);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference || GetBaseType(property) == null)
            {
                return EditorGUIUtility.singleLineHeight * 2f;
            }

            float height = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue == null || !property.isExpanded)
            {
                return height;
            }

            foreach (SerializedProperty child in GetDirectChildren(property))
            {
                height += VerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            }

            return height;
        }

        private static void DrawHeader(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            Type baseType,
            Type currentType)
        {
            Rect headerRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new(headerRect.x, headerRect.y, EditorGUIUtility.labelWidth, headerRect.height);
            Rect popupRect = new(labelRect.xMax, headerRect.y, headerRect.xMax - labelRect.xMax, headerRect.height);

            if (currentType == null)
            {
                EditorGUI.LabelField(labelRect, label);
            }
            else
            {
                property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, true);
            }

            string typeName = currentType == null ? "None" : ObjectNames.NicifyVariableName(currentType.Name);
            if (EditorGUI.DropdownButton(popupRect, new GUIContent(typeName), FocusType.Keyboard))
            {
                ShowTypeMenu(property, baseType, currentType);
            }
        }

        private static void DrawChildren(Rect position, SerializedProperty property)
        {
            float y = position.y + EditorGUIUtility.singleLineHeight + VerticalSpacing;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (SerializedProperty child in GetDirectChildren(property))
                {
                    float childHeight = EditorGUI.GetPropertyHeight(child, true);
                    Rect childRect = new(position.x, y, position.width, childHeight);
                    EditorGUI.PropertyField(childRect, child, true);
                    y += childHeight + VerticalSpacing;
                }
            }
        }

        private static IEnumerable<SerializedProperty> GetDirectChildren(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            int childDepth = property.depth + 1;
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.depth == childDepth)
                {
                    yield return iterator.Copy();
                }
            }
        }

        private static void ShowTypeMenu(SerializedProperty property, Type baseType, Type currentType)
        {
            GenericMenu menu = new();
            SerializedProperty propertyCopy = property.Copy();

            menu.AddItem(new GUIContent("None"), currentType == null, () => SetManagedReference(propertyCopy, null));
            menu.AddSeparator(string.Empty);

            Type[] concreteTypes = GetConcreteTypes(baseType);
            if (concreteTypes.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No serializable subclasses found"));
            }

            foreach (Type concreteType in concreteTypes)
            {
                Type selectedType = concreteType;
                string menuPath = GetMenuPath(baseType, selectedType);
                menu.AddItem(
                    new GUIContent(menuPath),
                    selectedType == currentType,
                    () => SetManagedReference(propertyCopy, selectedType));
            }

            menu.ShowAsContext();
        }

        private static void SetManagedReference(SerializedProperty property, Type selectedType)
        {
            UnityEngine.Object[] targets = property.serializedObject.targetObjects;
            Undo.RecordObjects(targets, "Change subclass");

            foreach (UnityEngine.Object target in targets)
            {
                SerializedObject serializedTarget = new(target);
                SerializedProperty targetProperty = serializedTarget.FindProperty(property.propertyPath);
                if (targetProperty == null)
                {
                    continue;
                }

                targetProperty.managedReferenceValue = selectedType == null
                    ? null
                    : Activator.CreateInstance(selectedType, true);
                targetProperty.isExpanded = selectedType != null;
                serializedTarget.ApplyModifiedProperties();
            }
        }

        private static bool SharesReferenceWithEarlierElement(SerializedProperty element)
        {
            long referenceId = element.managedReferenceId;
            if (referenceId == ManagedReferenceUtility.RefIdNull || referenceId == ManagedReferenceUtility.RefIdUnknown)
            {
                return false;
            }

            if (!TryGetCollectionIndex(element.propertyPath, out string collectionPath, out int index) || index == 0)
            {
                return false;
            }

            SerializedProperty collection = element.serializedObject.FindProperty(collectionPath);
            if (collection == null || !collection.isArray)
            {
                return false;
            }

            int count = Mathf.Min(index, collection.arraySize);
            for (int i = 0; i < count; i++)
            {
                if (collection.GetArrayElementAtIndex(i).managedReferenceId == referenceId)
                {
                    return true;
                }
            }

            return false;
        }

        // Without undo: the change that made the shared reference ("+", duplicate) is already on the undo stack, and an
        // undo record here would re-create the copy right after every undo, so undo could never step past it.
        private static void DetachSharedReference(SerializedProperty property)
        {
            foreach (UnityEngine.Object target in property.serializedObject.targetObjects)
            {
                SerializedObject serializedTarget = new(target);
                SerializedProperty targetProperty = serializedTarget.FindProperty(property.propertyPath);
                if (targetProperty == null || !SharesReferenceWithEarlierElement(targetProperty))
                {
                    continue;
                }

                targetProperty.managedReferenceValue = CloneManagedReference(targetProperty.managedReferenceValue);
                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static object CloneManagedReference(object source)
        {
            Type type = source.GetType();
            object clone = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null) != null
                ? Activator.CreateInstance(type, true)
                : System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);

            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), clone);
            return clone;
        }

        /// <summary>Splits "path.Array.data[3]" into "path" and 3; false when the property isn't an array element.</summary>
        private static bool TryGetCollectionIndex(string propertyPath, out string collectionPath, out int index)
        {
            collectionPath = null;
            index = -1;

            if (!propertyPath.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }

            int markerIndex = propertyPath.LastIndexOf(ArrayDataMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            int indexStart = markerIndex + ArrayDataMarker.Length;
            if (!int.TryParse(propertyPath.Substring(indexStart, propertyPath.Length - 1 - indexStart), out index))
            {
                return false;
            }

            collectionPath = propertyPath.Substring(0, markerIndex);
            return true;
        }

        private static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
            {
                return collectionType.GetElementType();
            }

            if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return collectionType.GetGenericArguments()[0];
            }

            return collectionType;
        }

        private static Type[] GetConcreteTypes(Type baseType)
        {
            if (ConcreteTypesCache.TryGetValue(baseType, out Type[] cachedTypes))
            {
                return cachedTypes;
            }

            List<Type> types = new();

            if (CanCreate(baseType))
            {
                types.Add(baseType);
            }

            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (CanCreate(type))
                {
                    types.Add(type);
                }
            }

            types.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            Type[] result = types.ToArray();
            ConcreteTypesCache.Add(baseType, result);
            return result;
        }

        private static bool CanCreate(Type type)
        {
            return type is { IsClass: true, IsAbstract: false }
                && !type.ContainsGenericParameters
                && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                && type.IsDefined(typeof(SerializableAttribute), false)
                && type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null) != null;
        }

        private static string GetMenuPath(Type baseType, Type concreteType)
        {
            string typeName = ObjectNames.NicifyVariableName(concreteType.Name);
            if (string.IsNullOrEmpty(concreteType.Namespace) || concreteType.Namespace == baseType.Namespace)
            {
                return typeName;
            }

            return concreteType.Namespace.Replace('.', '/') + "/" + typeName;
        }

        private Type GetBaseType(SerializedProperty property)
        {
            SubclassDrawerAttribute drawerAttribute = (SubclassDrawerAttribute)attribute;
            if (drawerAttribute.BaseType != null)
            {
                return drawerAttribute.BaseType;
            }

            // For an array / List<T> field Unity passes the collection's FieldInfo while drawing one of its elements.
            Type fieldType = fieldInfo?.FieldType;
            if (fieldType != null && TryGetCollectionIndex(property.propertyPath, out _, out _))
            {
                fieldType = GetCollectionElementType(fieldType);
            }

            if (fieldType != null && fieldType != typeof(object))
            {
                return fieldType;
            }

            return ResolveManagedReferenceFieldType(property.managedReferenceFieldTypename);
        }

        private static Type ResolveManagedReferenceFieldType(string fieldTypeName)
        {
            if (string.IsNullOrEmpty(fieldTypeName))
            {
                return null;
            }

            int separatorIndex = fieldTypeName.IndexOf(' ');
            if (separatorIndex <= 0 || separatorIndex >= fieldTypeName.Length - 1)
            {
                return null;
            }

            string assemblyName = fieldTypeName.Substring(0, separatorIndex);
            string typeName = fieldTypeName.Substring(separatorIndex + 1);
            return Type.GetType($"{typeName}, {assemblyName}");
        }
    }
}
