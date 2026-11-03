using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Utils.Gizmo
{
    /// <summary>
    /// Сектор круга: зона обзора, угол атаки, конус. Направления — мировые оси,
    /// TransformRotation — по повороту объекта.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoArcDrawer")]
    public class ArcGizmoDrawer : ShapeGizmoDrawer
    {
        [FormerlySerializedAs("draw_Direction")]
        public DrawDirection direction = DrawDirection.Forward;

        [FormerlySerializedAs("arcRadius")]
        public float radius = 1f;

        [FormerlySerializedAs("arcAngle")]
        [Range(0f, 360f)] public float angle = 180f;

#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            GetAxes(transform, out Vector3 normal, out Vector3 forward);

            Vector3 center = transform.position + offset;
            Vector3 from = Quaternion.AngleAxis(-angle * 0.5f, normal) * forward;

            // Заливка и контур — вокруг одной оси, иначе при направлении не вперёд они расходились.
            if (DrawsShape)
            {
                UnityEditor.Handles.color = shapeColor;
                UnityEditor.Handles.DrawSolidArc(center, normal, from, angle, radius);
            }

            if (DrawsWire)
            {
                UnityEditor.Handles.color = wireColor;
                UnityEditor.Handles.DrawWireArc(center, normal, from, angle, radius);
            }
        }

        private void GetAxes(Transform transform, out Vector3 normal, out Vector3 forward)
        {
            switch (direction)
            {
                case DrawDirection.Backward: normal = Vector3.up; forward = Vector3.back; break;
                case DrawDirection.Left: normal = Vector3.up; forward = Vector3.left; break;
                case DrawDirection.Right: normal = Vector3.up; forward = Vector3.right; break;
                case DrawDirection.Up: normal = Vector3.forward; forward = Vector3.up; break;
                case DrawDirection.Down: normal = Vector3.forward; forward = Vector3.down; break;
                case DrawDirection.TransformRotation: normal = transform.up; forward = transform.forward; break;
                default: normal = Vector3.up; forward = Vector3.forward; break;
            }
        }
#endif
    }
}
