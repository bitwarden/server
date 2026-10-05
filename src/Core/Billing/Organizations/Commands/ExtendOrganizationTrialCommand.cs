using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.Commands;

using static StripeConstants;

public interface IExtendOrganizationTrialCommand
{
    /// <summary>
    /// Pushes the organization's trialing Stripe subscription's trial end back by <paramref name="days"/> days.
    /// </summary>
    /// <returns>The new trial end date (UTC) on success.</returns>
    Task<BillingCommandResult<DateTime>> Run(Organization organization, int days);
}

public class ExtendOrganizationTrialCommand(
    ILogger<ExtendOrganizationTrialCommand> logger,
    IStripeAdapter stripeAdapter,
    IOrganizationService organizationService) : BaseBillingCommand<ExtendOrganizationTrialCommand>(logger), IExtendOrganizationTrialCommand
{
    private readonly ILogger<ExtendOrganizationTrialCommand> _logger = logger;
    protected override Conflict DefaultConflict => new("We had a problem extending this trial. Please try again.");

    public Task<BillingCommandResult<DateTime>> Run(Organization organization, int days) => HandleAsync<DateTime>(async () =>
    {
        var daysValidationError = TrialExtensionPolicy.ValidateDays(days);
        if (daysValidationError != null)
        {
            return new BadRequest(daysValidationError);
        }

        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return new BadRequest(TrialExtensionPolicy.NoSubscriptionMessage);
        }

        var subscription = await stripeAdapter.GetSubscriptionAsync(
            organization.GatewaySubscriptionId,
            new SubscriptionGetOptions { Expand = ["test_clock"] });

        if (TrialExtensionPolicy.GetIneligibilityReason(subscription) is { } ineligibilityReason)
        {
            return new BadRequest(ineligibilityReason);
        }

        var newTrialEnd = subscription!.TrialEnd!.Value.AddDays(days);

        await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            TrialEnd = newTrialEnd,
            ProrationBehavior = ProrationBehavior.None
        });

        // Audit the Stripe mutation as soon as it succeeds. The expiration sync below is best-effort (see the catch),
        // and the trail for the extension that already happened must not depend on it.
        _logger.LogInformation(
            "{Command}: Extended trial for subscription ({SubscriptionId}) of organization ({OrganizationId}) by {Days} days",
            CommandName, subscription.Id, organization.Id, days);

        try
        {
            // Written synchronously so a subsequent admin Edit save can't overwrite it with the stale value before the webhook lands.
            await organizationService.UpdateExpirationDateAsync(organization.Id, newTrialEnd);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "{Command}: Extended trial for subscription ({SubscriptionId}) of organization ({OrganizationId}) to {NewTrialEnd:u} but failed to sync the expiration date; relying on the subscription.updated webhook",
                CommandName, subscription.Id, organization.Id, newTrialEnd);
        }

        return newTrialEnd;
    });
}
