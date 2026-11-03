using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Utils.Gizmo
{
    /// <summary>Линия через трансформы сцены: маршрут патруля, порядок точек.</summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoPathDrawer")]
    public class PathGizmoDrawer : GizmosDrawer
    {
        public List<Transform> points = new List<Transform>();

        [FormerlySerializedAs("gizmoColorLine")]
        public Color lineColor = DefaultShapeColor;

        [FormerlySerializedAs("gizmoColorWire")]
        public Color pointColor = DefaultWireColor;

        public float sphereRadius = 1f;

#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Transform point = points[i];

                if (point == null)
                    continue;

                Gizmos.color = pointColor;
                Gizmos.DrawWireSphere(point.position, sphereRadius);

                if (i + 1 < points.Count && points[i + 1] != null)
                {
                    Gizmos.color = lineColor;
                    Gizmos.DrawLine(point.position, points[i + 1].position);
                }
            }
        }
#endif
    }
}
