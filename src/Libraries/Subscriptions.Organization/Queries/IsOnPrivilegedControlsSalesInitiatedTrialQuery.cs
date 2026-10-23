using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Queries;

internal interface IIsOnPrivilegedControlsSalesInitiatedTrialQuery
{
    /// <summary>
    /// Whether Privileged Controls is on without a Privileged Controls seat line on the Stripe subscription.
    /// An organization with no subscription id counts as a trial.
    /// </summary>
    /// <remarks>Run before any Privileged Controls command; the commands don't detect trials.</remarks>
    /// <exception cref="ConflictException">Thrown when the organization's subscription id is no longer in Stripe.</exception>
    Task<bool> Run(OrganizationEntity organization);
}

internal sealed class IsOnPrivilegedControlsSalesInitiatedTrialQuery(
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ILogger<IsOnPrivilegedControlsSalesInitiatedTrialQuery> logger) : IIsOnPrivilegedControlsSalesInitiatedTrialQuery
{
    private const string InvalidSubscriptionMessage =
        "Your organization's subscription is in an invalid state. Please contact support for assistance.";

    public async Task<bool> Run(OrganizationEntity organization)
    {
        if (!organization.UsePam)
        {
            return false;
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsPrivilegedControls)
        {
            return false;
        }

        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return true;
        }

        var subscription = await GetSubscriptionOrThrowAsync(organization);

        return organization.IsOnPrivilegedControlsSalesInitiatedTrial(plan, subscription.HasPrivilegedControlsSeats());
    }

    private async Task<Subscription> GetSubscriptionOrThrowAsync(OrganizationEntity organization)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogError("Subscription ({SubscriptionId}) for organization ({OrganizationId}) was not found",
                organization.GatewaySubscriptionId, organization.Id);
            throw new ConflictException(InvalidSubscriptionMessage);
        }
    }
}
