namespace Bit.Core.Billing.Services;

public interface IStripeSyncService
{
    Task UpdateCustomerEmailAddress(string gatewayCustomerId, string emailAddress);
}
