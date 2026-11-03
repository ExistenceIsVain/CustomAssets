using System.Collections.Generic;
using UnityEngine;
using Utils.Gizmo;

namespace Utils.Samples.Gizmo
{
    /// <summary>
    /// Префаб GizmoExample — враг с разметкой зон: радиус агро, конус обзора, направление,
    /// подпись и свой ExampleClass. В префабе разметка уже настроена; при добавлении компонента
    /// на новый объект (или ПКМ → Reset) она заполняется из кода. Дальше её можно править
    /// в инспекторе GizmosObject. Гизмо видны в окне Scene и в Game при включённой кнопке Gizmos.
    /// </summary>
    [RequireComponent(typeof(GizmosObject))]
    [AddComponentMenu("Utils/Samples/Gizmo Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        [SerializeField] private float aggroRadius = 3f;
        [SerializeField] private float viewDistance = 2.5f;
        [SerializeField, Range(0f, 360f)] private float viewAngle = 90f;
        [SerializeField] private float turnSpeed = 30f;

        private bool turning;

        private void Reset()
        {
            GizmosObject gizmos = GetComponent<GizmosObject>();
            List<GizmosDrawer> drawers = gizmos.GetDrawers();
            drawers.Clear();

            drawers.Add(new SphereGizmoDrawer
            {
                drawStyle = GizmosDrawer.DrawStyle.OnlyWire,
                radius = aggroRadius,
                wireColor = new Color(1f, 0.3f, 0.2f, 0.8f)
            });

            drawers.Add(new ArcGizmoDrawer
            {
                direction = GizmosDrawer.DrawDirection.TransformRotation,
                drawStyle = GizmosDrawer.DrawStyle.ShapeAndWire,
                radius = viewDistance,
                angle = viewAngle,
                shapeColor = new Color(1f, 0.9f, 0.2f, 0.2f)
            });

            drawers.Add(new ArrowGizmoDrawer { length = 1.5f });

            // disableOnSelect: подпись не мешает хэндлам, пока объект выделен.
            drawers.Add(new TextGizmoDrawer { label = name, offset = Vector3.up * 2f, disableOnSelect = true });

            drawers.Add(new ExampleClass { drawStyle = GizmosDrawer.DrawStyle.ShapeAndWire });

#if UNITY_EDITOR
            // Список правится в обход SerializedObject — без SetDirty сцена не сохранила бы изменения.
            UnityEditor.EditorUtility.SetDirty(gizmos);
#endif
        }

        private void Update()
        {
            // Конус обзора и стрелка поворачиваются вместе с объектом (DrawDirection.TransformRotation).
            if (turning)
                transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f);
        }

        public string Title => "Gizmo";

        public void DrawPanel()
        {
            GUILayout.Label("Разметка — в окне Scene или в Game с кнопкой Gizmos.\nОтрисовщики — в инспекторе GizmosObject.");

            if (DemoGUI.Button(turning ? "Stop Turning" : "Turn")) turning = !turning;
        }
    }
}
