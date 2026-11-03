using System.Collections.Generic;
using UnityEngine;

namespace Utils.Samples
{
    /// <summary>
    /// Меню демо-сцены: вкладка на каждый пример (IDemoPanel), камера подлетает к выбранному,
    /// в редакторе он же выделяется — инспектор показывает его поля.
    /// Положи префаб DemoMenu в любую сцену с префабами примеров — у них появятся кнопки.
    /// </summary>
    [AddComponentMenu("Utils/Samples/Demo Menu")]
    public class DemoMenu : MonoBehaviour
    {
        [Tooltip("Пусто — Camera.main.")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 3f, -7f);
        [SerializeField, Min(0.1f)] private float cameraSpeed = 5f;
        [SerializeField, Min(100f)] private float panelWidth = 320f;

        private readonly List<IDemoPanel> panels = new List<IDemoPanel>();
        private string[] titles = new string[0];
        private int selected = -1;

        private void Start()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IDemoPanel panel)
                    panels.Add(panel);
            }

            // Слева направо, как стоят в сцене.
            panels.Sort((a, b) => PanelTransform(a).position.x.CompareTo(PanelTransform(b).position.x));

            titles = new string[panels.Count];
            for (int i = 0; i < panels.Count; i++)
                titles[i] = panels[i].Title;

            if (panels.Count > 0)
                Select(0);
        }

        private void LateUpdate()
        {
            if (targetCamera == null || selected < 0 || PanelTransform(panels[selected]) == null)
                return;

            Vector3 focus = PanelTransform(panels[selected]).position;
            Transform cam = targetCamera.transform;
            float t = 1f - Mathf.Exp(-cameraSpeed * Time.unscaledDeltaTime);

            cam.position = Vector3.Lerp(cam.position, focus + cameraOffset, t);
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(focus - cam.position), t);
        }

        private void OnGUI()
        {
            if (panels.Count == 0)
            {
                GUILayout.Label("В сцене нет примеров с IDemoPanel.");
                return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, Screen.width - 20f, Screen.height - 20f));

            int next = GUILayout.Toolbar(selected, titles, GUILayout.Height(28f));
            if (next != selected)
                Select(next);

            GUILayout.Space(6f);

            // Пример мог быть уничтожен (например, кнопкой в своей же панели).
            if (PanelTransform(panels[selected]) != null)
            {
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(panelWidth));
                GUILayout.Label($"<b>{panels[selected].Title}</b>", RichLabel);
                panels[selected].DrawPanel();
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }

        private void Select(int index)
        {
            selected = index;

#if UNITY_EDITOR
            if (PanelTransform(panels[index]) != null)
                UnityEditor.Selection.activeGameObject = PanelTransform(panels[index]).gameObject;
#endif
        }

        private static Transform PanelTransform(IDemoPanel panel)
        {
            // Unity-null: уничтоженный компонент сравнивается с null только через UnityEngine.Object.
            var behaviour = panel as MonoBehaviour;
            return behaviour != null ? behaviour.transform : null;
        }

        private static GUIStyle richLabel;

        private static GUIStyle RichLabel => richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };
    }
}
