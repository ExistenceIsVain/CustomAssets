using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Utils.Gizmo
{
    /// <summary>Повторяет BoxCollider или SphereCollider объекта: триггеры видны и без выделения.</summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoColliderDrawer")]
    public class ColliderGizmoDrawer : ShapeGizmoDrawer
    {
#if UNITY_EDITOR
        public override void Draw(Transform transform)
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            if (transform.TryGetComponent(out BoxCollider box))
            {
                if (DrawsShape)
                {
                    Gizmos.color = shapeColor;
                    Gizmos.DrawCube(box.center + offset, box.size);
                }

                if (DrawsWire)
                {
                    Gizmos.color = wireColor;
                    Gizmos.DrawWireCube(box.center + offset, box.size);
                }

                return;
            }

            if (transform.TryGetComponent(out SphereCollider sphere))
            {
                if (DrawsShape)
                {
                    Gizmos.color = shapeColor;
                    Gizmos.DrawSphere(sphere.center + offset, sphere.radius);
                }

                if (DrawsWire)
                {
                    Gizmos.color = wireColor;
                    Gizmos.DrawWireSphere(sphere.center + offset, sphere.radius);
                }

                return;
            }

            Gizmos.matrix = Matrix4x4.identity;
            DrawProblem(transform, "Collider gizmo: нужен BoxCollider или SphereCollider");
        }
#endif
    }
}
