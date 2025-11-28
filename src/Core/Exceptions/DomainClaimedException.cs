namespace Bit.Core.Exceptions;

#nullable enable

public class DomainClaimedException : Exception
{
    public DomainClaimedException()
        : base("The domain is not available to be claimed.")
    {

    }
}
