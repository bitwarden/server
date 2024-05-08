using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;
using Stripe;

namespace Bit.Core.Models.Business;

/// <summary>
/// A model representing the data required to upgrade from one subscription to another using a <see cref="CompleteSubscriptionUpdate"/>.
/// </summary>
public class SubscriptionData
{
    public StaticStore.Plan Plan { get; init; }
    public int PurchasedPasswordManagerSeats { get; init; }
    public bool SubscribedToSecretsManager { get; set; }
    public int? PurchasedSecretsManagerSeats { get; init; }
    public int? PurchasedAdditionalSecretsManagerServiceAccounts { get; init; }
    public int PurchasedAdditionalStorage { get; init; }
}

public class CompleteSubscriptionUpdate : SubscriptionUpdate
{
    private readonly SubscriptionData _currentSubscription;
    private readonly SubscriptionData _updatedSubscription;

    private readonly Dictionary<string, SubscriptionUpdateType> _subscriptionUpdateMap = new();

    private enum SubscriptionUpdateType
    {
        PasswordManagerSeats,
        SecretsManagerSeats,
        SecretsManagerServiceAccounts,
        Storage
    }

    /// <summary>
    /// A model used to generate the Stripe <see cref="SubscriptionItemOptions"/>
    /// necessary to both upgrade an organization's subscription and revert that upgrade
    /// in the case of an error.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to upgrade.</param>
    /// <param name="updatedSubscription">The updates you want to apply to the organization's subscription.</param>
    public CompleteSubscriptionUpdate(
        Organization organization,
        SubscriptionData updatedSubscription)
    {
        _currentSubscription = GetSubscriptionDataFor(organization);
        _updatedSubscription = updatedSubscription;
    }

    protected override List<string> PlanIds => new()
    {
        GetPasswordManagerPlanId(_updatedSubscription.Plan),
        _updatedSubscription.Plan.SecretsManager.StripeSeatPlanId,
        _updatedSubscription.Plan.SecretsManager.StripeServiceAccountPlanId,
        _updatedSubscription.Plan.PasswordManager.StripeStoragePlanId
    };

    /// <summary>
    /// Generates the <see cref="SubscriptionItemOptions"/> necessary to revert an <see cref="Organization"/>'s
    /// <see cref="Subscription"/> upgrade in the case of an error.
    /// </summary>
    /// <param name="subscription">The organization's <see cref="Subscription"/>.</param>
    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        var subscriptionItemOptions = new List<SubscriptionItemOptions>
        {
            GetPasswordManagerOptions(subscription, _updatedSubscription, _currentSubscription)
        };

        if (_updatedSubscription.SubscribedToSecretsManager || _currentSubscription.SubscribedToSecretsManager)
        {
            subscriptionItemOptions.Add(GetSecretsManagerOptions(subscription, _updatedSubscription, _currentSubscription));

            if (_updatedSubscription.PurchasedAdditionalSecretsManagerServiceAccounts != 0 ||
                _currentSubscription.PurchasedAdditionalSecretsManagerServiceAccounts != 0)
            {
                subscriptionItemOptions.Add(GetServiceAccountsOptions(subscription, _updatedSubscription, _currentSubscription));
            }
        }

        if (_updatedSubscription.PurchasedAdditionalStorage != 0 || _currentSubscription.PurchasedAdditionalStorage != 0)
        {
            subscriptionItemOptions.Add(GetStorageOptions(subscription, _updatedSubscription, _currentSubscription));
        }

        return subscriptionItemOptions;
    }

    /*
     * This is almost certainly overkill. If we trust the data in the Vault DB, we should just be able to
     * compare the _currentSubscription against the _updatedSubscription to see if there are any differences.
     * However, for the sake of ensuring we're checking against the Stripe subscription itself, I'll leave this
     * included for now.
     */
    /// <summary>
    /// Checks whether the updates provided in the <see cref="CompleteSubscriptionUpdate"/>'s constructor
    /// are actually different than the organization's current <see cref="Subscription"/>.
    /// </summary>
    /// <param name="subscription">The organization's <see cref="Subscription"/>.</param>
    public override bool UpdateNeeded(Subscription subscription)
    {
        var upgradeItemsOptions = UpgradeItemsOptions(subscription);

        foreach (var subscriptionItemOptions in upgradeItemsOptions)
        {
            var success = _subscriptionUpdateMap.TryGetValue(subscriptionItemOptions.Price, out var updateType);

            if (!success)
            {
                return false;
            }

            var updateNeeded = updateType switch
            {
                SubscriptionUpdateType.PasswordManagerSeats => ContainsUpdatesBetween(
                    GetPasswordManagerPlanId(_currentSubscription.Plan),
                    subscriptionItemOptions),
                SubscriptionUpdateType.SecretsManagerSeats => ContainsUpdatesBetween(
                    _currentSubscription.Plan.SecretsManager.StripeSeatPlanId,
                    subscriptionItemOptions),
                SubscriptionUpdateType.SecretsManagerServiceAccounts => ContainsUpdatesBetween(
                    _currentSubscription.Plan.SecretsManager.StripeServiceAccountPlanId,
                    subscriptionItemOptions),
                SubscriptionUpdateType.Storage => ContainsUpdatesBetween(
                    _currentSubscription.Plan.PasswordManager.StripeStoragePlanId,
                    subscriptionItemOptions),
                _ => false
            };

            if (updateNeeded)
            {
                return true;
            }
        }

        return false;

        bool ContainsUpdatesBetween(string currentPlanId, SubscriptionItemOptions options)
        {
            var subscriptionItem = FindSubscriptionItem(subscription, currentPlanId);

            return (subscriptionItem.Plan.Id != options.Plan && subscriptionItem.Price.Id != options.Plan) ||
                   subscriptionItem.Quantity != options.Quantity ||
                   subscriptionItem.Deleted != options.Deleted;
        }
    }

    /// <summary>
    /// Generates the <see cref="SubscriptionItemOptions"/> necessary to upgrade an <see cref="Organization"/>'s
    /// <see cref="Subscription"/>.
    /// </summary>
    /// <param name="subscription">The organization's <see cref="Subscription"/>.</param>
    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var subscriptionItemOptions = new List<SubscriptionItemOptions>
        {
            GetPasswordManagerOptions(subscription, _currentSubscription, _updatedSubscription)
        };

        if (_currentSubscription.SubscribedToSecretsManager || _updatedSubscription.SubscribedToSecretsManager)
        {
            subscriptionItemOptions.Add(GetSecretsManagerOptions(subscription, _currentSubscription, _updatedSubscription));

            if (_currentSubscription.PurchasedAdditionalSecretsManagerServiceAccounts != 0 ||
                _updatedSubscription.PurchasedAdditionalSecretsManagerServiceAccounts != 0)
            {
                subscriptionItemOptions.Add(GetServiceAccountsOptions(subscription, _currentSubscription, _updatedSubscription));
            }
        }

        if (_currentSubscription.PurchasedAdditionalStorage != 0 || _updatedSubscription.PurchasedAdditionalStorage != 0)
        {
            subscriptionItemOptions.Add(GetStorageOptions(subscription, _currentSubscription, _updatedSubscription));
        }

        return subscriptionItemOptions;
    }

    private SubscriptionItemOptions GetPasswordManagerOptions(
        Subscription subscription,
        SubscriptionData from,
        SubscriptionData to)
    {
        var currentPlanId = GetPasswordManagerPlanId(from.Plan);

        var subscriptionItem = FindSubscriptionItem(subscription, currentPlanId);

        if (subscriptionItem == null)
        {
            throw new GatewayException("Could not find Password Manager subscription");
        }

        var updatedPlanId = GetPasswordManagerPlanId(to.Plan);

        _subscriptionUpdateMap[updatedPlanId] = SubscriptionUpdateType.PasswordManagerSeats;

        return new SubscriptionItemOptions
        {
            Id = subscriptionItem.Id,
            Price = updatedPlanId,
            Quantity = IsNonSeatBasedPlan(to.Plan) ? 1 : to.PurchasedPasswordManagerSeats
        };
    }

    private SubscriptionItemOptions GetSecretsManagerOptions(
        Subscription subscription,
        SubscriptionData from,
        SubscriptionData to)
    {
        var currentPlanId = from.Plan?.SecretsManager?.StripeSeatPlanId;

        var subscriptionItem = !string.IsNullOrEmpty(currentPlanId)
            ? FindSubscriptionItem(subscription, currentPlanId)
            : null;

        var updatedPlanId = to.Plan.SecretsManager.StripeSeatPlanId;

        _subscriptionUpdateMap[updatedPlanId] = SubscriptionUpdateType.SecretsManagerSeats;

        return new SubscriptionItemOptions
        {
            Id = subscriptionItem?.Id,
            Price = updatedPlanId,
            Quantity = to.PurchasedSecretsManagerSeats,
            Deleted = subscriptionItem?.Id != null && to.PurchasedSecretsManagerSeats == 0
                ? true
                : null
        };
    }

    private SubscriptionItemOptions GetServiceAccountsOptions(
        Subscription subscription,
        SubscriptionData from,
        SubscriptionData to)
    {
        var currentPlanId = from.Plan?.SecretsManager?.StripeServiceAccountPlanId;

        var subscriptionItem = !string.IsNullOrEmpty(currentPlanId)
            ? FindSubscriptionItem(subscription, currentPlanId)
            : null;

        var updatedPlanId = to.Plan.SecretsManager.StripeServiceAccountPlanId;

        _subscriptionUpdateMap[updatedPlanId] = SubscriptionUpdateType.SecretsManagerServiceAccounts;

        return new SubscriptionItemOptions
        {
            Id = subscriptionItem?.Id,
            Price = updatedPlanId,
            Quantity = to.PurchasedAdditionalSecretsManagerServiceAccounts,
            Deleted = subscriptionItem?.Id != null && to.PurchasedAdditionalSecretsManagerServiceAccounts == 0
                ? true
                : null
        };
    }

    private SubscriptionItemOptions GetStorageOptions(
        Subscription subscription,
        SubscriptionData from,
        SubscriptionData to)
    {
        var currentPlanId = from.Plan.PasswordManager.StripeStoragePlanId;

        var subscriptionItem = FindSubscriptionItem(subscription, currentPlanId);

        var updatedPlanId = to.Plan.PasswordManager.StripeStoragePlanId;

        _subscriptionUpdateMap[updatedPlanId] = SubscriptionUpdateType.Storage;

        return new SubscriptionItemOptions
        {
            Id = subscriptionItem?.Id,
            Price = updatedPlanId,
            Quantity = to.PurchasedAdditionalStorage,
            Deleted = subscriptionItem?.Id != null && to.PurchasedAdditionalStorage == 0
                ? true
                : null
        };
    }

    private static SubscriptionData GetSubscriptionDataFor(Organization organization)
    {
        var plan = Utilities.StaticStore.GetPlan(organization.PlanType);

        return new SubscriptionData
        {
            Plan = plan,
            PurchasedPasswordManagerSeats = organization.Seats.HasValue
                ? organization.Seats.Value - plan.PasswordManager.BaseSeats
                : 0,
            SubscribedToSecretsManager = organization.UseSecretsManager,
            PurchasedSecretsManagerSeats = plan.SecretsManager is not null
                ? organization.SmSeats - plan.SecretsManager.BaseSeats
                : 0,
            PurchasedAdditionalSecretsManagerServiceAccounts = plan.SecretsManager is not null
                ? organization.SmServiceAccounts - plan.SecretsManager.BaseServiceAccount
                : 0,
            PurchasedAdditionalStorage = organization.MaxStorageGb.HasValue
                ? organization.MaxStorageGb.Value - (plan.PasswordManager.BaseStorageGb ?? 0) :
                0
        };
    }
}
