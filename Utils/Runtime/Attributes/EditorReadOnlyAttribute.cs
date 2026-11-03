using System;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Показывает поле в инспекторе, но запрещает его править.
    /// Работает и на public-поле, и на [SerializeField] private — Unity рисует любое сериализованное поле.
    /// Это подсказка «сюда не лезь», а не защита: значение всё ещё меняется кодом,
    /// через инспектор в режиме Debug, копипаст YAML и Preset.
    /// Не совмещать с другими дровверами ([Range], [MinMaxRange], [SubclassDrawer]):
    /// у поля работает только один PropertyDrawer, и Unity не гарантирует, какой именно победит.
    /// На массиве или списке применяется к элементам — размер и кнопки +/- останутся активными.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EditorReadOnlyAttribute : PropertyAttribute
    {
    }
}
