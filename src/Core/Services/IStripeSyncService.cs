namespace Bit.Core.Services;

public interface IStripeSyncService
{
    Task UpdateCustomerEmailAddress(string gatewayCustomerId, string emailAddress);
}
