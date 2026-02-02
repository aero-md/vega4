using System;

namespace Core.CustomCommandAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RequireSuperAdminAttribute : Attribute
    {
        public bool Required { get; } = true;

        public RequireSuperAdminAttribute(){}
    }
}