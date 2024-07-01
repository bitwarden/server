namespace Bit.Core.Billing;

public class BillingException(
    string clientFriendlyMessage,
    string internalMessage = null,
    Exception innerException = null) : Exception(internalMessage, innerException)
{
    public string ClientFriendlyMessage { get; set; } = clientFriendlyMessage;
}
