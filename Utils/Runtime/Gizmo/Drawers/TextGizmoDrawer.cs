using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Utils.Gizmo
{
    /// <summary>Подпись в сцене: заметка для левел-дизайна прямо на объекте.</summary>
    [Serializable]
    [MovedFrom(true, "Helpers.Gizmo", "Assembly-CSharp", "gizmoTextDrawer")]
    public class TextGizmoDrawer : GizmosDrawer
    {
        [TextArea] public string label;
        public Color textColor = Color.white;
        public int textSize = 10;

        [FormerlySerializedAs("customPosition")]
        public Vector3 offset = Vector3.zero;

#if UNITY_EDITOR
        private static GUIStyle style;

        public override void Draw(Transform transform)
        {
            style ??= new GUIStyle();
            style.fontSize = textSize;
            style.normal.textColor = textColor;

            UnityEditor.Handles.Label(transform.position + offset, label, style);
        }
#endif
    }
}
