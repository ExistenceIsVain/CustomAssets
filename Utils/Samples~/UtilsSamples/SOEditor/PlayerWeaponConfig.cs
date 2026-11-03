using UnityEngine;

namespace Utils.Samples.SOEditor
{
    /// <summary>Weapon / Player.</summary>
    [SOSubGroup("Player")]
    [CreateAssetMenu(menuName = "Utils Samples/SO Editor/Player Weapon", fileName = "PlayerWeapon")]
    public class PlayerWeaponConfig : WeaponConfig
    {
        [Min(0)] public int price = 100;
    }
}
