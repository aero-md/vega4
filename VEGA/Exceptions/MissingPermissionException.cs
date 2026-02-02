using NetCord.Services;
using Resources;

namespace Exceptions;

public class MissingPermissionException : Exception
{
    public MissingPermissionsResult MissingPerm {get; set;}

    public MissingPermissionException(MissingPermissionsResult missingPerm) 
        : base(Strings.Exceptions.MissingPermission)
    {
        MissingPerm = missingPerm;
    }
}