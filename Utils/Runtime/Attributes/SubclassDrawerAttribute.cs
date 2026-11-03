using System;
using UnityEngine;

namespace Utils
{
    public class SubclassDrawerAttribute : PropertyAttribute
    {
        public Type BaseType { get; }

        public SubclassDrawerAttribute() { }

        public SubclassDrawerAttribute(Type baseType)
        {
            BaseType = baseType;
        }
    }
}
