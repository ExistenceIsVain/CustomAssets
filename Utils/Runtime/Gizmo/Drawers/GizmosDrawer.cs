using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Utils.Gizmo
{
    /// <summary>
    /// Один элемент разметки на <see cref="GizmosObject"/>. Наследник рисует себя в <see cref="Draw"/>.
    ///
    /// Поля сериализуются всегда, а не только в редакторе: иначе раскладка данных в сборке игры
    /// расходилась бы с той, что записана в сцене, и Unity писал бы ошибки при загрузке.
    /// Под UNITY_EDITOR — только сама отрисовка.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "GizmosDrawer")]
    public abstract class GizmosDrawer
    {
        public enum DrawStyle
        {
            OnlyShape,
            OnlyWire,
            ShapeAndWire
        }

        public enum DrawScale
        {
            Custom,
            GameObject,
            GameObjectAndCustom
        }

        public enum DrawDirection
        {
            Forward,
            Backward,
            Left,
            Right,
            Up,
            Down,
            TransformRotation
        }

        public static readonly Color DefaultShapeColor = new Color(0f, 0.9f, 1f, 0.5f);
        public static readonly Color DefaultWireColor = new Color(0.1098f, 1f, 0f, 0.5f);

        [FormerlySerializedAs("drawer_enabled")]
        public bool enabled = true;

        [Tooltip("Не рисовать, пока объект выделен: чтобы разметка не закрывала хэндлы.")]
        public bool disableOnSelect;

#if UNITY_EDITOR
        /// <summary>Вызывается из OnDrawGizmos. Матрица Gizmos уже сброшена в identity.</summary>
        public abstract void Draw(Transform transform);

        /// <summary>Интерактивные хэндлы в окне сцены, пока объект выделен.</summary>
        public virtual void OnSceneGUI(GizmosObject owner) { }

        /// <summary>
        /// Подпись вместо фигуры, когда рисовать нечего (нет нужного компонента).
        /// В консоль не пишем: OnDrawGizmos зовётся каждую перерисовку сцены.
        /// </summary>
        protected static void DrawProblem(Transform transform, string message)
        {
            Color color = UnityEditor.Handles.color;
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(transform.position, message);
            UnityEditor.Handles.color = color;
        }
#endif
    }

    /// <summary>Фигура с заливкой и/или контуром и смещением от объекта.</summary>
    [Serializable]
    public abstract class ShapeGizmoDrawer : GizmosDrawer
    {
        [FormerlySerializedAs("drawStylized")]
        public DrawStyle drawStyle = DrawStyle.OnlyShape;

        [FormerlySerializedAs("gizmoColorShape")]
        public Color shapeColor = DefaultShapeColor;

        [FormerlySerializedAs("gizmoColorWire")]
        public Color wireColor = DefaultWireColor;

        [FormerlySerializedAs("customPosition")]
        public Vector3 offset = Vector3.zero;

        protected bool DrawsShape => drawStyle != DrawStyle.OnlyWire;
        protected bool DrawsWire => drawStyle != DrawStyle.OnlyShape;
    }

    /// <summary>
    /// Фигура с масштабом: свой, объекта или сумма. С rotateToObject рисуется в локальных
    /// координатах объекта — поворот и масштаб берутся из его матрицы.
    /// </summary>
    [Serializable]
    public abstract class ScaledShapeGizmoDrawer : ShapeGizmoDrawer
    {
        public bool rotateToObject;

        [FormerlySerializedAs("draw_Scale")]
        public DrawScale drawScale = DrawScale.Custom;

        public Vector3 customScale = Vector3.one;

#if UNITY_EDITOR
        /// <summary>
        /// Выставляет матрицу Gizmos и возвращает центр и размер фигуры в её координатах.
        /// В локальном режиме масштаб объекта уже в матрице, поэтому «масштаб объекта» = 1.
        /// </summary>
        protected void Prepare(Transform transform, out Vector3 center, out Vector3 size)
        {
            Vector3 objectScale = rotateToObject ? Vector3.one : transform.lossyScale;

            size = drawScale switch
            {
                DrawScale.GameObject => objectScale,
                DrawScale.GameObjectAndCustom => objectScale + customScale,
                _ => customScale
            };

            if (rotateToObject)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                center = offset;
            }
            else
            {
                center = transform.position + offset;
            }
        }
#endif
    }
}
