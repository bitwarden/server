namespace Bit.Core.Exceptions;

public class DuplicateDomainException : Exception
{
    public DuplicateDomainException()
        : base("A domain already exists for this organization.") { }
}
