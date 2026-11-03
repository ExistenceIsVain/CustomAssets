using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Utils.Gizmo
{
    /// <summary>Меш из MeshFilter объекта. Без rotateToObject рисуется без поворота объекта.</summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoMeshDrawer")]
    public class MeshGizmoDrawer : ScaledShapeGizmoDrawer
    {
#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            if (!transform.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
            {
                DrawProblem(transform, "Mesh gizmo: нет MeshFilter с мешем");
                return;
            }

            Prepare(transform, out Vector3 center, out Vector3 size);
            Mesh mesh = meshFilter.sharedMesh;

            if (DrawsShape)
            {
                Gizmos.color = shapeColor;
                Gizmos.DrawMesh(mesh, center, Quaternion.identity, size);
            }

            if (DrawsWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireMesh(mesh, center, Quaternion.identity, size);
            }
        }
#endif
    }
}
