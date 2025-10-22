namespace Bit.RustSDK;

/// <summary>
/// Exception thrown when the Rust SDK operations fail
/// </summary>
public class RustSdkException : Exception
{
    public RustSdkException() : base("An error occurred in the Rust SDK operation")
    {
    }

    public RustSdkException(string message) : base(message)
    {
    }

    public RustSdkException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
