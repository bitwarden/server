namespace Bit.Core.Billing.Caches;

public interface ISetupIntentCache
{
    Task<string> Get(Guid subscriberId);

    Task Remove(Guid subscriberId);

    Task Set(Guid subscriberId, string setupIntentId);
}
