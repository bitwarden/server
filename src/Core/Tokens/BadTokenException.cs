namespace Bit.Core.Tokens;

public class BadTokenException : Exception
{
    public BadTokenException() { }

    public BadTokenException(string message)
        : base(message) { }
}
