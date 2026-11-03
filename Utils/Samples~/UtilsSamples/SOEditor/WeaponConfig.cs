using UnityEngine;

namespace Utils.Samples.SOEditor
{
    /// <summary>
    /// База оружия: задаёт группу «Weapon» для себя и всех наследников. Ассет этого класса
    /// лежит прямо в группе, без сабгруппы. Открой Tools > Scriptable Object Editor.
    /// </summary>
    [SOGroup("Weapon")]
    [CreateAssetMenu(menuName = "Utils Samples/SO Editor/Weapon", fileName = "Weapon")]
    public class WeaponConfig : ScriptableObject
    {
        [Min(0)] public int damage = 10;
        [SteppedRange(0.1f, 3f, 0.1f)] public float cooldown = 1f;
    }
}
