using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Commands;

using static StripeConstants;

public interface IRestartSubscriptionCommand
{
    Task<BillingCommandResult<None>> Run(
        ISubscriber subscriber);
}

public class RestartSubscriptionCommand(
    ILogger<RestartSubscriptionCommand> logger,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : BaseBillingCommand<RestartSubscriptionCommand>(logger), IRestartSubscriptionCommand
{
    public Task<BillingCommandResult<None>> Run(
        ISubscriber subscriber) => HandleAsync<None>(async () =>
    {
        var existingSubscription = await subscriberService.GetSubscription(subscriber);

        if (existingSubscription is not { Status: SubscriptionStatus.Canceled })
        {
            return new BadRequest("Cannot restart a subscription that is not canceled.");
        }

        await RestartSubscriptionAsync(subscriber, existingSubscription);

        return new None();
    });

    private Task RestartSubscriptionAsync(
        ISubscriber subscriber,
        Subscription canceledSubscription) => subscriber switch
        {
            Organization organization => RestartOrganizationSubscriptionAsync(organization, canceledSubscription),
            _ => throw new NotSupportedException("Only organization subscriptions can be restarted")
        };

    private async Task RestartOrganizationSubscriptionAsync(
        Organization organization,
        Subscription canceledSubscription)
    {
        var plans = await pricingClient.ListPlans();

        var oldPlan = plans.FirstOrDefault(plan => plan.Type == organization.PlanType);

        if (oldPlan == null)
        {
            throw new ConflictException("Could not find plan for organization's plan type");
        }

        var newPlan = oldPlan.Disabled
            ? plans.FirstOrDefault(plan =>
                plan.ProductTier == oldPlan.ProductTier &&
                plan.IsAnnual == oldPlan.IsAnnual &&
                !plan.Disabled)
            : oldPlan;

        if (newPlan == null)
        {
            throw new ConflictException("Could not find the current, enabled plan for organization's tier and cadence");
        }

        if (newPlan.Type != oldPlan.Type)
        {
            organization.PlanType = newPlan.Type;
            organization.Plan = newPlan.Name;
            organization.SelfHost = newPlan.HasSelfHost;
            organization.UsePolicies = newPlan.HasPolicies;
            organization.UseGroups = newPlan.HasGroups;
            organization.UseDirectory = newPlan.HasDirectory;
            organization.UseEvents = newPlan.HasEvents;
            organization.UseTotp = newPlan.HasTotp;
            organization.Use2fa = newPlan.Has2fa;
            organization.UseApi = newPlan.HasApi;
            organization.UseSso = newPlan.HasSso;
            organization.UseOrganizationDomains = newPlan.HasOrganizationDomains;
            organization.UseKeyConnector = newPlan.HasKeyConnector;
            organization.UseScim = newPlan.HasScim;
            organization.UseResetPassword = newPlan.HasResetPassword;
            organization.UsersGetPremium = newPlan.UsersGetPremium;
            organization.UseCustomPermissions = newPlan.HasCustomPermissions;
        }

        var items = new List<SubscriptionItemOptions>();

        // Password Manager
        var passwordManagerItem = canceledSubscription.Items.FirstOrDefault(item =>
            item.Price.Id == (oldPlan.HasNonSeatBasedPasswordManagerPlan()
                ? oldPlan.PasswordManager.StripePlanId
                : oldPlan.PasswordManager.StripeSeatPlanId));

        if (passwordManagerItem == null)
        {
            throw new ConflictException("Organization's subscription does not have a Password Manager subscription item.");
        }

        items.Add(new SubscriptionItemOptions
        {
            Price = newPlan.PasswordManager.StripeSeatPlanId,
            Quantity = passwordManagerItem.Quantity
        });

        // Storage
        var storageItem = canceledSubscription.Items.FirstOrDefault(
            item => item.Price.Id == oldPlan.PasswordManager.StripeStoragePlanId);

        if (storageItem != null)
        {
            items.Add(new SubscriptionItemOptions
            {
                Price = newPlan.PasswordManager.StripeStoragePlanId,
                Quantity = storageItem.Quantity
            });
        }

        // Secrets Manager
        var secretsManagerItem =
            canceledSubscription.Items.FirstOrDefault(item => item.Price.Id == oldPlan.SecretsManager.StripeSeatPlanId);

        if (secretsManagerItem != null)
        {
            items.Add(new SubscriptionItemOptions
            {
                Price = newPlan.SecretsManager.StripeSeatPlanId,
                Quantity = secretsManagerItem.Quantity
            });
        }

        // Service Accounts
        var serviceAccountsItem = canceledSubscription.Items.FirstOrDefault(item => item.Price.Id == oldPlan.SecretsManager.StripeServiceAccountPlanId);

        if (serviceAccountsItem != null)
        {
            items.Add(new SubscriptionItemOptions
            {
                Price = newPlan.SecretsManager.StripeServiceAccountPlanId,
                Quantity = serviceAccountsItem.Quantity
            });
        }

        var options = new SubscriptionCreateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
            CollectionMethod = CollectionMethod.ChargeAutomatically,
            Customer = canceledSubscription.CustomerId,
            Items = items,
            Metadata = canceledSubscription.Metadata,
            OffSession = true,
            TrialPeriodDays = 0
        };

        var subscription = await stripeAdapter.SubscriptionCreateAsync(options);

        organization.GatewaySubscriptionId = subscription.Id;
        organization.Enabled = true;
        organization.ExpirationDate = subscription.GetCurrentPeriodEnd();
        organization.RevisionDate = DateTime.UtcNow;

        await organizationRepository.ReplaceAsync(organization);
    }
}
