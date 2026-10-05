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

        if (subscription is not { Status: SubscriptionStatus.Trialing, TrialEnd: not null })
        {
            return new BadRequest(TrialExtensionPolicy.NotTrialingMessage);
        }

        if (TrialExtensionPolicy.GetRemainingDays(subscription) >= TrialExtensionPolicy.MaxRemainingDaysForExtension)
        {
            return new BadRequest(TrialExtensionPolicy.TooManyDaysRemainingMessage);
        }

        // Stripe discourages direct subscription updates while a schedule is attached; the schedule would own the trial end.
        if (!string.IsNullOrEmpty(subscription.ScheduleId))
        {
            return new BadRequest(TrialExtensionPolicy.ScheduleAttachedMessage);
        }

        var newTrialEnd = subscription.TrialEnd.Value.AddDays(days);

        await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            TrialEnd = newTrialEnd,
            ProrationBehavior = ProrationBehavior.None
        });

        // Audit the Stripe mutation as soon as it succeeds. The expiration sync below can fail independently and
        // surface a retryable error to the admin, and the trail for the extension that already happened must survive that.
        _logger.LogInformation(
            "{Command}: Extended trial for subscription ({SubscriptionId}) of organization ({OrganizationId}) by {Days} days",
            CommandName, subscription.Id, organization.Id, days);

        // Written synchronously so a subsequent admin Edit save can't overwrite it with the stale value before the webhook lands.
        await organizationService.UpdateExpirationDateAsync(organization.Id, newTrialEnd);

        return newTrialEnd;
    });
}
