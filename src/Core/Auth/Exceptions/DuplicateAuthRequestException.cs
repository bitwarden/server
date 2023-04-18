namespace Bit.Core.Auth.Exceptions;

public class DuplicateAuthRequestException : Exception
{
    public DuplicateAuthRequestException()
        : base("An authentication request with the same device already exists.")
    {

    }
}
