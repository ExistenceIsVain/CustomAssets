using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Utils.EditorTools
{
    /// <summary>
    /// Проверяет все [SteppedRange] в проекте при перезагрузке скриптов и пишет ошибки в консоль.
    ///
    /// Плашка в инспекторе показывает проблему только у открытого объекта, а опечатку
    /// в редко открываемом компоненте так можно не увидеть годами. Проверка по сборкам
    /// ловит её сразу после компиляции, ещё до того, как кто-то полезет настраивать поле.
    ///
    /// TypeCache видит поля во всех сборках, включая asmdef проекта, и уже проиндексирован
    /// редактором — перебирать типы через reflection не нужно.
    /// </summary>
    public static class SteppedRangeValidator
    {
        [DidReloadScripts]
        private static void Validate()
        {
            foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<SteppedRangeAttribute>())
            {
                SteppedRangeAttribute range = field.GetCustomAttribute<SteppedRangeAttribute>();

                if (range == null || !range.HasError)
                    continue;

                Debug.LogError(
                    $"{field.DeclaringType?.Name}.{field.Name}: {range.ErrorMessage} " +
                    $"Написано: [SteppedRange({range.RawMin}f, {range.RawMax}f, {range.RawStep}f)]");
            }
        }
    }
}
