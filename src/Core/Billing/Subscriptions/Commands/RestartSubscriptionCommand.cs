using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Entities;
using Bit.Core.Repositories;
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
    IGetOrganizationRestartQuery getOrganizationRestartQuery,
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
        var restart = await getOrganizationRestartQuery.Run(organization, canceledSubscription);
        var newPlan = restart.Plan;

        if (newPlan.Type != organization.PlanType)
        {
            organization.PlanType = newPlan.Type;
            organization.Plan = newPlan.Name;
            organization.SelfHost = newPlan.HasSelfHost;
            organization.UsePolicies = newPlan.HasPolicies;
            organization.UseMyItems = newPlan.HasMyItems;
            organization.UseInviteLinks = newPlan.HasInviteLinks;
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
            organization.UseRiskInsights = newPlan.HasRiskInsights;
        }

        var items = restart.Items
            .Select(item => new SubscriptionItemOptions { Price = item.PriceId, Quantity = item.Quantity })
            .ToList();

        var options = new SubscriptionCreateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
            BillingMode = new SubscriptionBillingModeOptions { Type = StripeConstants.BillingMode.Classic },
            CollectionMethod = CollectionMethod.ChargeAutomatically,
            Customer = canceledSubscription.CustomerId,
            Items = items,
            Metadata = canceledSubscription.Metadata,
            OffSession = true,
            TrialPeriodDays = 0
        };

        var subscription = await stripeAdapter.CreateSubscriptionAsync(options);

        organization.GatewaySubscriptionId = subscription.Id;
        organization.Enabled = true;
        organization.ExpirationDate = subscription.GetCurrentPeriodEnd();
        organization.RevisionDate = DateTime.UtcNow;

        await organizationRepository.ReplaceAsync(organization);
    }
}
