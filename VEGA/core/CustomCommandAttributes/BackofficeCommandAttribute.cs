using System;

namespace Core.CustomCommandAttributes
{
    /// <summary>
    /// Marks a command as a backoffice/management command.
    /// Commands with this attribute will only be registered on the backoffice guild specified in configuration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class BackofficeCommandAttribute : Attribute
    {
        public BackofficeCommandAttribute() { }
    }
}
