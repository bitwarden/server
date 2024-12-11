using Stripe;

namespace Bit.Core.Models.Business;

public class StorageSubscriptionUpdate : SubscriptionUpdate
{
    private long? _prevStorage;
    private readonly string _plan;
    private readonly long? _additionalStorage;
    protected override List<string> PlanIds => new() { _plan };

    public StorageSubscriptionUpdate(string plan, long? additionalStorage)
    {
        _plan = plan;
        _additionalStorage = additionalStorage;
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var item = FindSubscriptionItem(subscription, PlanIds.Single());
        _prevStorage = item?.Quantity ?? 0;
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = _plan,
                Quantity = _additionalStorage,
                Deleted = (item?.Id != null && _additionalStorage == 0) ? true : (bool?)null,
            },
        };
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        if (!_prevStorage.HasValue)
        {
            throw new Exception("Unknown previous value, must first call UpgradeItemsOptions");
        }

        var item = FindSubscriptionItem(subscription, PlanIds.Single());
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = _plan,
                Quantity = _prevStorage.Value,
                Deleted = _prevStorage.Value == 0 ? true : (bool?)null,
            },
        };
    }
}
