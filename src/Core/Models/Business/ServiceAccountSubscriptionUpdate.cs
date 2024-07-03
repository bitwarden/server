using Bit.Core.AdminConsole.Entities;
using Stripe;

namespace Bit.Core.Models.Business;

public class ServiceAccountSubscriptionUpdate : SubscriptionUpdate
{
    private long? _prevServiceAccounts;
    private readonly StaticStore.Plan _plan;
    private readonly long? _additionalServiceAccounts;
    protected override List<string> PlanIds => new() { _plan.SecretsManager.StripeServiceAccountPlanId };

    public ServiceAccountSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? additionalServiceAccounts)
    {
        _plan = plan;
        _additionalServiceAccounts = additionalServiceAccounts;
        _prevServiceAccounts = organization.SmServiceAccounts ?? 0;
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var item = FindSubscriptionItem(subscription, PlanIds.Single());
        _prevServiceAccounts = item?.Quantity ?? 0;
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = PlanIds.Single(),
                Quantity = _additionalServiceAccounts,
                Deleted = (item?.Id != null && _additionalServiceAccounts == 0) ? true : (bool?)null,
            }
        };
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        var item = FindSubscriptionItem(subscription, PlanIds.Single());
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = PlanIds.Single(),
                Quantity = _prevServiceAccounts,
                Deleted = _prevServiceAccounts == 0 ? true : (bool?)null,
            }
        };
    }
}
