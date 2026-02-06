namespace Bit.Core.Exceptions;

#nullable enable

public class InvalidEmailException : Exception
{
    public InvalidEmailException()
        : base("Invalid email.")
    {

    }
}
