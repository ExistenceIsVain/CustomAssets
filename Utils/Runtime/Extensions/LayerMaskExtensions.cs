using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Проверки слоёв. Маску держать в [SerializeField] LayerMask или собрать один раз
    /// через LayerMask.GetMask("Player", "Enemy") и закешировать: GetMask ищет слои по имени
    /// и при каждом вызове выделяет массив под params.
    /// </summary>
    public static class LayerMaskExtensions
    {
        /// <summary>Входит ли слой с этим номером (0–31) в маску.</summary>
        public static bool Contains(this LayerMask mask, int layer)
        {
            return layer >= 0 && layer < 32 && (mask.value & (1 << layer)) != 0;
        }

        /// <summary>Входит ли слой объекта в маску.</summary>
        public static bool Contains(this LayerMask mask, GameObject gameObject)
        {
            return mask.Contains(gameObject.layer);
        }

        /// <summary>Входит ли слой объекта компонента (коллайдера, трансформа…) в маску.</summary>
        public static bool Contains(this LayerMask mask, Component component)
        {
            return mask.Contains(component.gameObject.layer);
        }

        /// <summary>
        /// Номер слоя, если в маске ровно один слой, иначе -1.
        /// Нужен, чтобы выставить gameObject.layer из поля LayerMask в инспекторе.
        /// </summary>
        public static int ToSingleLayer(this LayerMask mask)
        {
            int value = mask.value;

            // Ноль или больше одного бита — однозначного слоя нет.
            if (value == 0 || (value & (value - 1)) != 0)
                return -1;

            int layer = 0;
            while ((value & 1) == 0)
            {
                value >>= 1;
                layer++;
            }

            return layer;
        }
    }
}
