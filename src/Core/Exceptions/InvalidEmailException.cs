namespace Bit.Core.Exceptions;

public class InvalidEmailException : Exception
{
    public InvalidEmailException()
        : base("Invalid email.") { }
}
