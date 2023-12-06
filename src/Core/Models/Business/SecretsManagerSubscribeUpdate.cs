using Bit.Core.AdminConsole.Entities;
using Stripe;

namespace Bit.Core.Models.Business;

public class SecretsManagerSubscribeUpdate : SubscriptionUpdate
{
    private readonly StaticStore.Plan _plan;
    private readonly long? _additionalSeats;
    private readonly long? _additionalServiceAccounts;
    private readonly int _previousSeats;
    private readonly int _previousServiceAccounts;
    protected override List<string> PlanIds => new() { _plan.SecretsManager.StripeSeatPlanId, _plan.SecretsManager.StripeServiceAccountPlanId };
    public SecretsManagerSubscribeUpdate(Organization organization, StaticStore.Plan plan, long? additionalSeats, long? additionalServiceAccounts)
    {
        _plan = plan;
        _additionalSeats = additionalSeats;
        _additionalServiceAccounts = additionalServiceAccounts;
        _previousSeats = organization.SmSeats.GetValueOrDefault();
        _previousServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault();
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        var updatedItems = new List<SubscriptionItemOptions>();

        RemovePreviousSecretsManagerItems(updatedItems);

        return updatedItems;
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var updatedItems = new List<SubscriptionItemOptions>();

        AddNewSecretsManagerItems(updatedItems);

        return updatedItems;
    }

    private void AddNewSecretsManagerItems(List<SubscriptionItemOptions> updatedItems)
    {
        if (_additionalSeats > 0)
        {
            updatedItems.Add(new SubscriptionItemOptions
            {
                Price = _plan.SecretsManager.StripeSeatPlanId,
                Quantity = _additionalSeats
            });
        }

        if (_additionalServiceAccounts > 0)
        {
            updatedItems.Add(new SubscriptionItemOptions
            {
                Price = _plan.SecretsManager.StripeServiceAccountPlanId,
                Quantity = _additionalServiceAccounts
            });
        }
    }

    private void RemovePreviousSecretsManagerItems(List<SubscriptionItemOptions> updatedItems)
    {
        updatedItems.Add(new SubscriptionItemOptions
        {
            Price = _plan.SecretsManager.StripeSeatPlanId,
            Quantity = _previousSeats,
            Deleted = _previousSeats == 0 ? true : (bool?)null,
        });

        updatedItems.Add(new SubscriptionItemOptions
        {
            Price = _plan.SecretsManager.StripeServiceAccountPlanId,
            Quantity = _previousServiceAccounts,
            Deleted = _previousServiceAccounts == 0 ? true : (bool?)null,
        });
    }
}
