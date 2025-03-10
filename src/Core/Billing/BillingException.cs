namespace Bit.Core.Billing;

public class BillingException(
    string response = null,
    string message = null,
    Exception innerException = null) : Exception(message, innerException)
{
    public const string DefaultMessage = "Something went wrong with your request. Please contact support.";

    public string Response { get; } = response ?? DefaultMessage;
}
