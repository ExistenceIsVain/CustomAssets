
namespace Utils
{
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
