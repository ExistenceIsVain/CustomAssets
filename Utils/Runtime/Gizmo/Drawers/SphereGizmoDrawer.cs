using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Utils.Gizmo
{
    /// <summary>
    /// Сфера: радиус слуха, зона агро. Масштаб объекта — половина длины lossyScale,
    /// то есть сфера, описанная вокруг единичного куба объекта.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoSphereDrawer")]
    public class SphereGizmoDrawer : ShapeGizmoDrawer
    {
        [FormerlySerializedAs("draw_Scale")]
        public DrawScale drawScale = DrawScale.Custom;

        [FormerlySerializedAs("sphereRadius")]
        public float radius = 1f;

#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            float objectRadius = transform.lossyScale.magnitude * 0.5f;

            float r = drawScale switch
            {
                DrawScale.GameObject => objectRadius,
                DrawScale.GameObjectAndCustom => objectRadius + radius,
                _ => radius
            };

            Vector3 center = transform.position + offset;

            if (DrawsShape)
            {
                Gizmos.color = shapeColor;
                Gizmos.DrawSphere(center, r);
            }

            if (DrawsWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireSphere(center, r);
            }
        }
#endif
    }
}
