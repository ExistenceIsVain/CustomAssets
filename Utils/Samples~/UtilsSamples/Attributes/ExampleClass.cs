using System;
using UnityEngine;

namespace Utils.Samples.Attributes
{
    /// <summary>
    /// База для [SubclassDrawer]: в инспекторе у поля появится выпадающий список наследников.
    /// Наследник должен быть [Serializable], не абстрактным и с конструктором без параметров.
    /// </summary>
    [Serializable]
    public abstract class ExampleClass
    {
        public abstract void Apply(GameObject target);
    }

    [Serializable]
    public class HealEffect : ExampleClass
    {
        [Min(0)] public int amount = 10;

        public override void Apply(GameObject target) => Debug.Log($"{target.name}: +{amount} HP");
    }

    [Serializable]
    public class BurnEffect : ExampleClass
    {
        [Min(0)] public int damagePerSecond = 3;
        [MinMaxRange(1f, 10f)] public Vector2 duration = new Vector2(2f, 4f);

        public override void Apply(GameObject target) =>
            Debug.Log($"{target.name}: горит {damagePerSecond}/с, {duration.x:F1}–{duration.y:F1} с");
    }

    /// <summary>
    /// Источник значений для [StringConstant]. Константы вложенного класса попадают в подменю
    /// с его именем, [Region] перед полем открывает подменю до следующего [Region].
    /// </summary>
    public static class ExampleConstants
    {
        [Region("Triggers")]
        public const string Attack = "Attack";
        public const string Dodge = "Dodge";

        [Region("Bools")]
        public const string IsBlocking = "IsBlocking";

        public static class Sounds
        {
            public const string Hit = "sfx_hit";
            public const string Swing = "sfx_swing";
        }
    }
}
