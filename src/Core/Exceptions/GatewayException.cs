namespace Bit.Core.Exceptions;

#nullable enable

public class GatewayException : Exception
{
    public GatewayException(string message, Exception? innerException = null)
        : base(message, innerException)
    { }
}
