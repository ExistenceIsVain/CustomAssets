using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Utils.Gizmo
{
    /// <summary>
    /// Стрелка направления: спавн, точка выхода, направление патруля. Направления — относительно
    /// объекта; useCustomDirection — мировой поворот из углов Эйлера.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoArrowDrawer")]
    public class ArrowGizmoDrawer : GizmosDrawer
    {
        [FormerlySerializedAs("draw_Direction")]
        public DrawDirection direction = DrawDirection.Forward;

        public bool useCustomDirection;
        public Vector3 customDirection = Vector3.zero;

        [FormerlySerializedAs("gizmoColorShape")]
        public Color color = DefaultShapeColor;

        [FormerlySerializedAs("arrowLenght")]
        public float length = 1f;

        [FormerlySerializedAs("customPosition")]
        public Vector3 offset = Vector3.zero;

#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            // ArrowHandleCap рисует только на Repaint; на других событиях вызов пустой.
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            Quaternion rotation = useCustomDirection
                ? Quaternion.Euler(customDirection)
                : GetRotation(transform);

            UnityEditor.Handles.color = color;
            UnityEditor.Handles.ArrowHandleCap(0, transform.position + offset, rotation, length, EventType.Repaint);
        }

        private Quaternion GetRotation(Transform transform)
        {
            if (direction == DrawDirection.TransformRotation)
                return transform.rotation;

            Vector3 forward = direction switch
            {
                DrawDirection.Backward => -transform.forward,
                DrawDirection.Left => -transform.right,
                DrawDirection.Right => transform.right,
                DrawDirection.Up => transform.up,
                DrawDirection.Down => -transform.up,
                _ => transform.forward
            };

            return Quaternion.LookRotation(forward);
        }
#endif
    }
}
