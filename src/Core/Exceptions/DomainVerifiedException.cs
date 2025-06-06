namespace Bit.Core.Exceptions;

#nullable enable

public class DomainVerifiedException : Exception
{
    public DomainVerifiedException()
        : base("Domain has already been verified.")
    {

    }
}
