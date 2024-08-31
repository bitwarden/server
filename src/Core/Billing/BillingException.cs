namespace Bit.Core.Billing;

public class BillingException(
    string response = null,
    string message = null,
    Exception innerException = null) : Exception(message, innerException)
{
    public string Response { get; } = response ?? "Something went wrong with your request. Please contact support.";
}
