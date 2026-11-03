using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Utils.Gizmo
{
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoCubeDrawer")]
    public class CubeGizmoDrawer : ScaledShapeGizmoDrawer
    {
#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            Prepare(transform, out Vector3 center, out Vector3 size);

            if (DrawsShape)
            {
                Gizmos.color = shapeColor;
                Gizmos.DrawCube(center, size);
            }

            if (DrawsWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireCube(center, size);
            }
        }
#endif
    }
}
