namespace Bit.Core.Billing;

public class BillingException(
    string responseMessage = null,
    string message = null,
    Exception innerException = null) : Exception(message, innerException)
{
    public string ResponseMessage { get; } = responseMessage ?? "Something went wrong with your request. Please contact support.";
}
