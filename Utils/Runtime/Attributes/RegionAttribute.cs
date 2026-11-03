using System;

namespace Utils
{
    // [AttributeUsage( AllowMultiple = false)]
    public class RegionAttribute : Attribute
    {
        public string Name { get; private set; }
    
        public RegionAttribute(string name)
        {
            Name = name;
        }
    }
}
