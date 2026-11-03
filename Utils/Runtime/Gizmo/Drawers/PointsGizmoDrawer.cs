using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Utils.Gizmo
{
    /// <summary>
    /// Список точек без трансформов: точки спавна, путь. Пока объект выделен, каждую точку
    /// можно тащить хэндлом в сцене.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoPointsDrawer")]
    public class PointsGizmoDrawer : GizmosDrawer
    {
        public List<Vector3> points = new List<Vector3>();

        [Tooltip("Точки в координатах объекта: двигаются вместе с ним.")]
        public bool localSpace = true;

        [FormerlySerializedAs("gizmoColorLine")]
        public Color lineColor = DefaultShapeColor;

        [FormerlySerializedAs("gizmoColorWire")]
        public Color pointColor = DefaultWireColor;

        public float sphereRadius = 0.5f;

#if UNITY_EDITOR
        private static GUIStyle labelStyle;

        public override void Draw(Transform transform)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 position = ToWorld(transform, points[i]);

                Gizmos.color = pointColor;
                Gizmos.DrawSphere(position, sphereRadius);

                if (i + 1 < points.Count)
                {
                    Gizmos.color = lineColor;
                    Gizmos.DrawLine(position, ToWorld(transform, points[i + 1]));
                }
            }
        }

        public override void OnSceneGUI(GizmosObject owner)
        {
            Transform transform = owner.transform;

            labelStyle ??= new GUIStyle { fontSize = 12 };
            labelStyle.normal.textColor = pointColor;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 position = ToWorld(transform, points[i]);

                UnityEditor.EditorGUI.BeginChangeCheck();
                Vector3 moved = UnityEditor.Handles.PositionHandle(position, Quaternion.identity);

                if (UnityEditor.EditorGUI.EndChangeCheck())
                {
                    UnityEditor.Undo.RecordObject(owner, "Move Point");
                    points[i] = localSpace ? transform.InverseTransformPoint(moved) : moved;
                }

                UnityEditor.Handles.Label(position + Vector3.up * (sphereRadius * 1.5f), $"[{i}]", labelStyle);
            }
        }

        private Vector3 ToWorld(Transform transform, Vector3 point)
        {
            return localSpace ? transform.TransformPoint(point) : point;
        }
#endif
    }
}
