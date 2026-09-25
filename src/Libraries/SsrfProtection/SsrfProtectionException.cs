namespace Bit.Core.Utilities;

/// <summary>
/// Exception thrown when an SSRF protection check fails.
/// </summary>
public class SsrfProtectionException : Exception
{
    public SsrfProtectionException(string message) : base(message) { }
    public SsrfProtectionException(string message, Exception innerException) : base(message, innerException) { }
}
