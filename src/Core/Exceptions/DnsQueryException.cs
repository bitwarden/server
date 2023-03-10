namespace Bit.Core.Exceptions;

public class DnsQueryException : Exception
{
    public DnsQueryException(string message)
        : base(message) { }
}
