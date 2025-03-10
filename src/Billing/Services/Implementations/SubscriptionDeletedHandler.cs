using Bit.Billing.Constants;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Services;
using Event = Stripe.Event;
namespace Bit.Billing.Services.Implementations;

public class SubscriptionDeletedHandler : ISubscriptionDeletedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IUserService _userService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;

    public SubscriptionDeletedHandler(
        IStripeEventService stripeEventService,
        IUserService userService,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationDisableCommand organizationDisableCommand)
    {
        _stripeEventService = stripeEventService;
        _userService = userService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationDisableCommand = organizationDisableCommand;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.SubscriptionDeleted"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true);
        var (organizationId, userId, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);
        var subCanceled = subscription.Status == StripeSubscriptionStatus.Canceled;

        const string providerMigrationCancellationComment = "Cancelled as part of provider migration to Consolidated Billing";
        const string addedToProviderCancellationComment = "Organization was added to Provider";

        if (!subCanceled)
        {
            return;
        }

        if (organizationId.HasValue)
        {
            if (!string.IsNullOrEmpty(subscription.CancellationDetails?.Comment) &&
                (subscription.CancellationDetails.Comment == providerMigrationCancellationComment ||
                 subscription.CancellationDetails.Comment.Contains(addedToProviderCancellationComment)))
            {
                return;
            }

            await _organizationDisableCommand.DisableAsync(organizationId.Value, subscription.CurrentPeriodEnd);
        }
        else if (userId.HasValue)
        {
            await _userService.DisablePremiumAsync(userId.Value, subscription.CurrentPeriodEnd);
        }
    }
}
