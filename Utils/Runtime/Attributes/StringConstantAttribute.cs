using System;
using UnityEngine;

namespace Utils
{
    [AttributeUsage(AttributeTargets.Field)]
    public class StringConstantAttribute : PropertyAttribute
    {
        public Type ConstantsType { get; }

        public StringConstantAttribute(Type constantsType)
        {
            ConstantsType = constantsType;
        }
    }
}
