namespace Bit.Core.Exceptions;

public class GatewayException : Exception
{
    public GatewayException(string message, Exception innerException = null)
        : base(message, innerException) { }
}
