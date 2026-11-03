using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Слайдер с шагом: как обычный [Range], но значение прилипает к кратным step.
    /// Например [SteppedRange(0f, 100f, 5f)] даёт 0, 5, 10, 15 и так далее.
    ///
    /// Нужен там, где дизайнеру важны круглые числа: проценты шанса, шаги баланса.
    /// Ввод числом с клавиатуры тоже округляется, иначе поле обходило бы шаг.
    ///
    /// Кривые аргументы не бросают исключение: конструктор зовётся из OnGUI при каждой
    /// отрисовке инспектора, и бросок оттуда сорвал бы отрисовку всего объекта, засыпав
    /// консоль повторами. Вместо этого значения зажимаются, а дровер пишет ошибку
    /// на месте самого поля — так видно, какое поле чинить.
    /// </summary>
    public class SteppedRangeAttribute : PropertyAttribute
    {
        public float Min { get; }
        public float Max { get; }
        public float Step { get; }

        /// <summary>Аргументы как их написали в коде — для сообщения об ошибке.</summary>
        public float RawMin { get; }
        public float RawMax { get; }
        public float RawStep { get; }

        /// <summary>Есть ли в атрибуте ошибка, о которой нужно сказать вслух.</summary>
        public bool HasError => ErrorMessage != null;

        /// <summary>Текст ошибки для инспектора или null, если аргументы корректны.</summary>
        public string ErrorMessage { get; }

        /// <param name="min">
        /// Левый край слайдера. От него же отсчитываются ступени, а не от нуля:
        /// иначе диапазон 3-100 с шагом 5 не дал бы выбрать сам 3.
        /// Если min больше max, границы меняются местами — но это ошибка, о ней скажет инспектор.
        /// </param>
        /// <param name="max">
        /// Правый край слайдера. Достижим всегда, даже когда шаг не делит диапазон нацело:
        /// у 0-100 с шагом 30 ступени идут 0-30-60-90, и без отдельного прилипания
        /// край дал бы 90 вместо 100.
        /// </param>
        /// <param name="step">
        /// Шаг округления. Не больше всего диапазона: иначе слайдер получил бы
        /// всего одно допустимое значение и перестал быть слайдером.
        /// Ноль и отрицательный шаг отключают прилипание — остаётся обычный Range.
        /// </param>
        public SteppedRangeAttribute(float min, float max, float step)
        {
            RawMin = min;
            RawMax = max;
            RawStep = step;

            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);

            float span = Max - Min;

            Step = step > span ? span : step;

            ErrorMessage = BuildError(min, max, step, span);
        }

        /// <summary>
        /// Первая найденная ошибка или null. Молча чинить нельзя: [SteppedRange(0, 100, 500)]
        /// почти наверняка опечатка, и слайдер с двумя положениями — не то, что имели в виду.
        /// </summary>
        private static string BuildError(float min, float max, float step, float span)
        {
            if (float.IsNaN(min) || float.IsNaN(max) || float.IsNaN(step))
                return "SteppedRange: аргументы не должны быть NaN.";

            if (Mathf.Approximately(span, 0f))
                return $"SteppedRange: пустой диапазон ({min} - {max}) — Min и Max совпадают.";

            if (min > max)
                return $"SteppedRange: Min ({min}) больше Max ({max}) — аргументы переставлены местами.";

            if (step < 0f)
                return $"SteppedRange: шаг ({step}) отрицательный.";

            if (step > span)
                return $"SteppedRange: шаг ({step}) больше диапазона ({min} - {max}). " +
                       $"Слайдер выдавал бы только {min} и {max}.";

            return null;
        }
    }
}
