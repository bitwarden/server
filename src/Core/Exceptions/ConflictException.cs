namespace Bit.Core.Exceptions;

#nullable enable

public class ConflictException : Exception
{
    public ConflictException() : base("Conflict.") { }
    public ConflictException(string message) : base(message) { }
}
