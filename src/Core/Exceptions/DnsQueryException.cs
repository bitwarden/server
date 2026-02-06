namespace Bit.Core.Exceptions;

#nullable enable

public class DnsQueryException : Exception
{
    public DnsQueryException(string message)
        : base(message) { }
}
