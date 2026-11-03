
namespace Utils
{
    /// <summary>
    /// Сабгруппа класса ScriptableObject в окне Tools > Scriptable Object Editor — второй уровень
    /// под группой [SOGroup]. Наследуется, наследник может задать свою. Без [SOGroup] в иерархии
    /// сабгруппа попадает в «Ungrouped».
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
    public class SOSubGroupAttribute : System.Attribute
    {
        public string SubGroupName { get; }

        public SOSubGroupAttribute(string subGroupName)
        {
            SubGroupName = subGroupName;
        }
    }
}
