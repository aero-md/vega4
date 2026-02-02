using NetCord.Services;
using Resources;

namespace Exceptions;

public class RequireSuperAdminException : Exception
{
    public RequireSuperAdminException() 
        : base(Strings.Exceptions.RequireSuperAdmin)
    {
    }
}