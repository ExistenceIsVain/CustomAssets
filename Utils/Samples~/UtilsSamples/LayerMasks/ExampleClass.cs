using System.Collections.Generic;
using UnityEngine;

namespace Utils.Samples.LayerMasks
{
    /// <summary>
    /// Обычный класс без MonoBehaviour: отбирает из найденных коллайдеров только те,
    /// чей слой входит в маску. Маска приходит снаружи — из поля компонента или конфига.
    /// </summary>
    public class ExampleClass
    {
        private readonly LayerMask targets;

        public ExampleClass(LayerMask targets)
        {
            this.targets = targets;
        }

        /// <summary>Все цели в радиусе. Слой проверяется у коллайдера — это Component.</summary>
        public void FindTargets(Vector3 center, float radius, List<Collider> result)
        {
            result.Clear();

            foreach (Collider collider in Physics.OverlapSphere(center, radius))
            {
                if (targets.Contains(collider))
                    result.Add(collider);
            }
        }

        /// <summary>Проверка по GameObject — для объектов без коллайдера.</summary>
        public bool IsTarget(GameObject gameObject) => targets.Contains(gameObject);
    }
}
