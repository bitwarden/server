namespace Bit.Core.Exceptions;

#nullable enable

public class DuplicateDomainException : Exception
{
    public DuplicateDomainException()
        : base("A domain already exists for this organization.")
    {

    }
}
