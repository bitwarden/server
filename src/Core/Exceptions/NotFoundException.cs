namespace Bit.Core.Exceptions;

#nullable enable

public class NotFoundException : Exception
{
    public NotFoundException() : base()
    { }

    public NotFoundException(string message)
        : base(message)
    { }
}
