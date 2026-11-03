using UnityEngine;

namespace Utils.Samples.SOEditor
{
    /// <summary>Сабгруппа без группы: попадает в Ungrouped / Consumables.</summary>
    [SOSubGroup("Consumables")]
    [CreateAssetMenu(menuName = "Utils Samples/SO Editor/Potion", fileName = "Potion")]
    public class PotionConfig : ScriptableObject
    {
        [Min(0)] public int heal = 25;
    }
}
