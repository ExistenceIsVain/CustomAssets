using System.Collections.Generic;
using UnityEngine;

namespace Utils.Samples.LayerMasks
{
    /// <summary>
    /// Префаб LayerMaskExample — зона-триггер, реагирующая только на слои из маски, и цели вокруг.
    /// В префабе целевой слой — встроенный Water, чтобы пример не требовал своих слоёв;
    /// в игре это будет свой слой вроде Enemy.
    /// Нужен коллайдер с Is Trigger, а у входящего объекта — Rigidbody (иначе Unity не шлёт OnTriggerEnter).
    /// В демо-сцене — кнопки на панели, в своей сцене — ПКМ по компоненту → Scan / Drop Target Ball / Drop Other Ball.
    /// </summary>
    [AddComponentMenu("Utils/Samples/LayerMask Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        [SerializeField] private LayerMask targets;
        [SerializeField] private float scanRadius = 4f;

        [Tooltip("Маска ровно с одним слоем — слой для шаров-целей. Обычно так задают слой спавнящимся объектам.")]
        [SerializeField] private LayerMask spawnLayer;

        [SerializeField] private float dropHeight = 4f;

        private readonly List<Collider> found = new List<Collider>();
        private ExampleClass scanner;
        private string lastEvent = "—";

        private void Awake()
        {
            scanner = new ExampleClass(targets);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!targets.Contains(other))
            {
                lastEvent = $"{other.name}: не цель, пропущен";
                return;
            }

            lastEvent = $"{other.name} вошёл в зону";
            Debug.Log($"{other.name} вошёл в зону {name}", this);
        }

        [ContextMenu("Scan")]
        private void Scan()
        {
            (scanner ??= new ExampleClass(targets)).FindTargets(transform.position, scanRadius, found);
            Debug.Log($"Целей в радиусе {scanRadius}: {found.Count}", this);
        }

        [ContextMenu("Drop Target Ball")]
        private void DropTargetBall()
        {
            int layer = spawnLayer.ToSingleLayer();

            if (layer < 0)
            {
                Debug.LogWarning("В Spawn Layer должен быть выбран ровно один слой.", this);
                return;
            }

            DropBall("Target Ball", layer);
        }

        [ContextMenu("Drop Other Ball")]
        private void DropOtherBall() => DropBall("Other Ball", 0);

        private void DropBall(string ballName, int layer)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = ballName;
            ball.layer = layer;
            ball.transform.position = transform.position + Vector3.up * dropHeight;
            ball.transform.localScale = Vector3.one * 0.5f;
            ball.AddComponent<Rigidbody>();

            Destroy(ball, 4f);
        }

        public string Title => "LayerMasks";

        public void DrawPanel()
        {
            GUILayout.Label($"Targets: {LayerNames(targets)}");
            GUILayout.Label($"Найдено при Scan: {found.Count}");

            foreach (Collider collider in found)
            {
                if (collider != null)
                    GUILayout.Label($"  • {collider.name}");
            }

            GUILayout.Label($"Триггер: {lastEvent}");

            if (DemoGUI.Button($"Scan (радиус {scanRadius})")) Scan();
            if (DemoGUI.Button("Drop Target Ball (spawnLayer)")) DropTargetBall();
            if (DemoGUI.Button("Drop Other Ball (Default)")) DropOtherBall();
        }

        private static string LayerNames(LayerMask mask)
        {
            var names = new List<string>();

            for (int layer = 0; layer < 32; layer++)
            {
                if (mask.Contains(layer))
                    names.Add(LayerMask.LayerToName(layer));
            }

            return names.Count > 0 ? string.Join(", ", names) : "Nothing";
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, scanRadius);
        }
    }
}
