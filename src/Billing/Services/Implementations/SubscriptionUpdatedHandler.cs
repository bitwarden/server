using Bit.Billing.Constants;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class SubscriptionUpdatedHandler : StripeWebhookHandler
{
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly IStripeEventService _stripeEventService;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IWebhookUtility _webhookUtility;

    public SubscriptionUpdatedHandler(IOrganizationService organizationService,
        IUserService userService,
        IStripeEventService stripeEventService,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IWebhookUtility webhookUtility)
    {
        _organizationService = organizationService;
        _userService = userService;
        _stripeEventService = stripeEventService;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _webhookUtility = webhookUtility;
    }
    protected override bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionUpdated);
    }

    protected override async Task<IActionResult> ProcessEvent(Event parsedEvent)
    {
        if (parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionUpdated))
        {
            var subscription = await _stripeEventService.GetSubscription(parsedEvent, true);
            var ids = _webhookUtility.GetIdsFromMetaData(subscription.Metadata);
            var organizationId = ids.Item1 ?? Guid.Empty;
            var userId = ids.Item2 ?? Guid.Empty;
            var subActive = subscription.Status == StripeSubscriptionStatus.Active;

            if (organizationId != Guid.Empty)
            {
                if (subActive)
                {
                    await _organizationService.EnableAsync(organizationId);
                }
                else
                {
                    await _organizationService.DisableAsync(organizationId, subscription.CurrentPeriodEnd);
                }

                await _organizationService.UpdateExpirationDateAsync(organizationId, subscription.CurrentPeriodEnd);

                if (_webhookUtility.IsSponsoredSubscription(subscription))
                {
                    await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(organizationId,
                        subscription.CurrentPeriodEnd);
                }
            }
            else if (userId != Guid.Empty)
            {
                if (subActive)
                {
                    await _userService.EnablePremiumAsync(userId, subscription.CurrentPeriodEnd);
                }
                else
                {
                    await _userService.DisablePremiumAsync(userId, subscription.CurrentPeriodEnd);
                }

                await _userService.UpdatePremiumExpirationAsync(userId, subscription.CurrentPeriodEnd);
            }
        }

        return new OkResult();
    }
}
