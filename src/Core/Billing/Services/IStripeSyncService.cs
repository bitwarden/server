namespace Bit.Core.Billing.Services;

public interface IStripeSyncService
{
    Task UpdateCustomerEmailAddressAsync(string gatewayCustomerId, string emailAddress);
}
