using UnityEngine;

namespace Utils.Samples.SOEditor
{
    /// <summary>
    /// Наследник EnemyWeaponConfig переопределяет сабгруппу: Weapon / Boss, а не Weapon / Enemy.
    /// Группу сменить нельзя — [SOGroup] с другим именем здесь игнорировался бы с предупреждением.
    /// </summary>
    [SOSubGroup("Boss")]
    [CreateAssetMenu(menuName = "Utils Samples/SO Editor/Boss Weapon", fileName = "BossWeapon")]
    public class BossWeaponConfig : EnemyWeaponConfig
    {
        [Min(1)] public int phases = 2;
    }
}
