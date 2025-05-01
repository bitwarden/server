namespace Bit.Core.Billing.Payment.Services;

public interface ISetupIntentCache
{
    Task<string> Get(Guid subscriberId);

    Task Remove(Guid subscriberId);

    Task Set(Guid subscriberId, string setupIntentId);
}
