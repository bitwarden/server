using Bit.Billing.Constants;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Extensions;
using Bit.Core.Services;
using Event = Stripe.Event;
namespace Bit.Billing.Services.Implementations;

public class SubscriptionDeletedHandler : ISubscriptionDeletedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IUserService _userService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;

    public SubscriptionDeletedHandler(
        IStripeEventService stripeEventService,
        IUserService userService,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationDisableCommand organizationDisableCommand,
        IProviderRepository providerRepository,
        IProviderService providerService,
        IProviderOrganizationRepository providerOrganizationRepository)
    {
        _stripeEventService = stripeEventService;
        _userService = userService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationDisableCommand = organizationDisableCommand;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _providerOrganizationRepository = providerOrganizationRepository;
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

            await _organizationDisableCommand.DisableAsync(organizationId.Value, subscription.GetCurrentPeriodEnd());
        }
        else if (providerId.HasValue)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId.Value);
            if (provider != null)
            {
                provider.Enabled = false;
                await _providerService.UpdateAsync(provider);

                // Disable all client organizations associated with this provider
                var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId.Value);
                if (providerOrganizations != null)
                {
                    foreach (var providerOrganization in providerOrganizations)
                    {
                        await _organizationDisableCommand.DisableAsync(
                            providerOrganization.OrganizationId,
                            subscription.GetCurrentPeriodEnd());
                    }
                }
            }
        }
        else if (userId.HasValue)
        {
            await _userService.DisablePremiumAsync(userId.Value, subscription.GetCurrentPeriodEnd());
        }
    }
}
