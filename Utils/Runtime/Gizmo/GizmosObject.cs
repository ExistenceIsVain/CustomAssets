using System.Collections.Generic;
using UnityEngine;

namespace Utils.Gizmo
{
    /// <summary>
    /// Разметка объекта гизмо без своего скрипта: сферы, дуги, стрелки, подписи, пути.
    /// Элементы добавляются в инспекторе кнопкой Add Drawer.
    /// </summary>
    [AddComponentMenu("Utils/Gizmos Object")]
    public class GizmosObject : MonoBehaviour
    {
        // Сериализуется и в сборке игры: поле под UNITY_EDITOR дало бы другую раскладку данных,
        // чем записана в сцене, и ошибки при загрузке. В сборке список просто не используется.
        [SerializeReference] private List<GizmosDrawer> gizmosDrawers = new List<GizmosDrawer>();

        public List<GizmosDrawer> GetDrawers() => gizmosDrawers;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            bool isSelected = UnityEditor.Selection.Contains(gameObject);

            foreach (GizmosDrawer drawer in gizmosDrawers)
            {
                // Пустой элемент остаётся, когда класс отрисовщика удалили или переименовали.
                if (drawer == null || !drawer.enabled || (isSelected && drawer.disableOnSelect))
                    continue;

                Gizmos.matrix = Matrix4x4.identity;
                drawer.Draw(transform);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
