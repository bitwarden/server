namespace Bit.Core.Exceptions;

public class DomainVerifiedException : Exception
{
    public DomainVerifiedException()
        : base("Domain has already been verified.") { }
}
