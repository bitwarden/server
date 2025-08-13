using Bit.Billing.Constants;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentSucceededHandler : IPaymentSucceededHandler
{
    private readonly ILogger<PaymentSucceededHandler> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly IUserService _userService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IProviderRepository _providerRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IOrganizationEnableCommand _organizationEnableCommand;
    private readonly IPricingClient _pricingClient;

    public PaymentSucceededHandler(
        ILogger<PaymentSucceededHandler> logger,
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IProviderRepository providerRepository,
        IOrganizationRepository organizationRepository,
        IStripeEventUtilityService stripeEventUtilityService,
        IUserService userService,
        IPushNotificationService pushNotificationService,
        IOrganizationEnableCommand organizationEnableCommand,
        IPricingClient pricingClient)
    {
        _logger = logger;
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _providerRepository = providerRepository;
        _organizationRepository = organizationRepository;
        _stripeEventUtilityService = stripeEventUtilityService;
        _userService = userService;
        _pushNotificationService = pushNotificationService;
        _organizationEnableCommand = organizationEnableCommand;
        _pricingClient = pricingClient;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.PaymentSucceeded"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (invoice.Status != StripeConstants.InvoiceStatus.Paid || invoice.BillingReason != "subscription_create")
        {
            return;
        }

        if (invoice.Parent is not { Type: "subscription_details" })
        {
            return;
        }

        var subscription = await _stripeFacade.GetSubscription(invoice.Parent.SubscriptionDetails.SubscriptionId);
        if (subscription?.Status != StripeSubscriptionStatus.Active)
        {
            return;
        }

        if (DateTime.UtcNow - invoice.Created < TimeSpan.FromMinutes(1))
        {
            await Task.Delay(5000);
        }

        var (organizationId, userId, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (providerId.HasValue)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId.Value);

            if (provider == null)
            {
                _logger.LogError(
                    "Received invoice.payment_succeeded webhook ({EventID}) for Provider ({ProviderID}) that does not exist",
                    parsedEvent.Id,
                    providerId.Value);

                return;
            }

            var teamsMonthly = await _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly);

            var enterpriseMonthly = await _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly);

            var teamsMonthlyLineItem =
                subscription.Items.Data.FirstOrDefault(item =>
                    item.Plan.Id == teamsMonthly.PasswordManager.StripeSeatPlanId);

            var enterpriseMonthlyLineItem =
                subscription.Items.Data.FirstOrDefault(item =>
                    item.Plan.Id == enterpriseMonthly.PasswordManager.StripeSeatPlanId);

            if (teamsMonthlyLineItem == null || enterpriseMonthlyLineItem == null)
            {
                _logger.LogError("invoice.payment_succeeded webhook ({EventID}) for Provider ({ProviderID}) indicates missing subscription line items",
                    parsedEvent.Id,
                    provider.Id);
            }
        }
        else if (organizationId.HasValue)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);

            if (organization == null)
            {
                return;
            }

            var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);

            if (subscription.Items.All(item => plan.PasswordManager.StripePlanId != item.Plan.Id))
            {
                return;
            }

            await _organizationEnableCommand.EnableAsync(organizationId.Value, subscription.GetCurrentPeriodEnd());
            await _pushNotificationService.PushSyncOrganizationStatusAsync(organization);
        }
        else if (userId.HasValue)
        {
            if (subscription.Items.All(i => i.Plan.Id != IStripeEventUtilityService.PremiumPlanId))
            {
                return;
            }

            await _userService.EnablePremiumAsync(userId.Value, subscription.GetCurrentPeriodEnd());
        }
    }
}
