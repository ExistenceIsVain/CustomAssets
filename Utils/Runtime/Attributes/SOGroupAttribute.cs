
namespace Utils
{
    /// <summary>
    /// Группа класса ScriptableObject в окне Tools > Scriptable Object Editor. Наследуется и задаётся
    /// один раз — самым базовым классом с атрибутом: наследник не может сменить группу, только
    /// уточнить её сабгруппой [SOSubGroup].
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
    public class SOGroupAttribute : System.Attribute
    {
        public string GroupName { get; }

        public SOGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }
}
