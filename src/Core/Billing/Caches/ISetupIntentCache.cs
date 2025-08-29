namespace Bit.Core.Billing.Caches;

public interface ISetupIntentCache
{
    Task<string?> GetSetupIntentIdForSubscriber(Guid subscriberId);
    Task<Guid?> GetSubscriberIdForSetupIntent(string setupIntentId);
    Task RemoveSetupIntentForSubscriber(Guid subscriberId);
    Task Set(Guid subscriberId, string setupIntentId);
}
