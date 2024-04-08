namespace Bit.Core.Billing;

public static class Utilities
{
    public const string BraintreeCustomerIdKey = "btCustomerId";

    public static BillingException ContactSupport(
        string internalMessage = null,
        Exception innerException = null) => new("Something went wrong with your request. Please contact support.",
        internalMessage, innerException);
}
