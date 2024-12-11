namespace Bit.Core.Exceptions;

public class DomainClaimedException : Exception
{
    public DomainClaimedException()
        : base("The domain is not available to be claimed.") { }
}
