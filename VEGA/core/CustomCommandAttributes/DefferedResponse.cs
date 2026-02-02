using System;

namespace Core.CustomCommandAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DefferedResponseAttribute : Attribute
    {
        public bool Required { get; } = true;
        public bool Ephemeral { get; set; } = false;

        public DefferedResponseAttribute(bool ephemeral = false)
        {
            Ephemeral = ephemeral;
        }
    }
}