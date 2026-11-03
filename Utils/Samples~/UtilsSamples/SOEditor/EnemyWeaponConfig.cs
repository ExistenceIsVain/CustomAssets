using UnityEngine;

namespace Utils.Samples.SOEditor
{
    /// <summary>Группа наследуется от WeaponConfig, сабгруппа своя: Weapon / Enemy.</summary>
    [SOSubGroup("Enemy")]
    [CreateAssetMenu(menuName = "Utils Samples/SO Editor/Enemy Weapon", fileName = "EnemyWeapon")]
    public class EnemyWeaponConfig : WeaponConfig
    {
        [Range(0f, 1f)] public float dropChance = 0.1f;
    }
}
