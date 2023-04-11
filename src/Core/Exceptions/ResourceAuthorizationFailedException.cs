namespace Bit.Core.Exceptions;

public class ResourceAuthorizationFailedException : Exception
{
    public ResourceAuthorizationFailedException()
        : base("You are not permitted to access this resource.")
    { }
}
