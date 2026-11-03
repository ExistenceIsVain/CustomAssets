using System;
using UnityEngine;
using Utils.Gizmo;

namespace Utils.Samples.Gizmo
{
    /// <summary>
    /// Свой отрисовщик для GizmosObject: крест с кругом — отметка точки интереса.
    /// Достаточно унаследоваться от GizmosDrawer (или ShapeGizmoDrawer, чтобы получить стиль,
    /// цвета и смещение) — класс сам появится в меню Add Drawer под именем «Example Class».
    /// Поля сериализуются всегда, а Draw — только в редакторе.
    /// </summary>
    [Serializable]
    public class ExampleClass : ShapeGizmoDrawer
    {
        [Min(0.01f)] public float size = 0.5f;

#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            Vector3 center = transform.position + offset;

            if (DrawsWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawLine(center - Vector3.right * size, center + Vector3.right * size);
                Gizmos.DrawLine(center - Vector3.forward * size, center + Vector3.forward * size);
                Gizmos.DrawLine(center - Vector3.up * size, center + Vector3.up * size);
            }

            if (DrawsShape)
            {
                UnityEditor.Handles.color = shapeColor;
                UnityEditor.Handles.DrawSolidDisc(center, Vector3.up, size * 0.5f);
            }
        }
#endif
    }
}
