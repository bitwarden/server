using Bit.Billing.Constants;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentSucceededHandler : IPaymentSucceededHandler
{
    private readonly ILogger<PaymentSucceededHandler> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IProviderRepository _providerRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IUserRepository _userRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;

    public PaymentSucceededHandler(
        ILogger<PaymentSucceededHandler> logger,
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IProviderRepository providerRepository,
        IOrganizationRepository organizationRepository,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext,
        IUserRepository userRepository,
        IStripeEventUtilityService stripeEventUtilityService,
        IUserService userService,
        IOrganizationService organizationService)
    {
        _logger = logger;
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _providerRepository = providerRepository;
        _organizationRepository = organizationRepository;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _userRepository = userRepository;
        _stripeEventUtilityService = stripeEventUtilityService;
        _userService = userService;
        _organizationService = organizationService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.PaymentSucceeded"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (!invoice.Paid || invoice.BillingReason != "subscription_create")
        {
            return;
        }

        var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);
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

            var teamsMonthly = StaticStore.GetPlan(PlanType.TeamsMonthly);

            var enterpriseMonthly = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

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

                return;
            }

            await _referenceEventService.RaiseEventAsync(new ReferenceEvent
            {
                Type = ReferenceEventType.Rebilled,
                Source = ReferenceEventSource.Provider,
                Id = provider.Id,
                PlanType = PlanType.TeamsMonthly,
                Seats = (int)teamsMonthlyLineItem.Quantity
            });

            await _referenceEventService.RaiseEventAsync(new ReferenceEvent
            {
                Type = ReferenceEventType.Rebilled,
                Source = ReferenceEventSource.Provider,
                Id = provider.Id,
                PlanType = PlanType.EnterpriseMonthly,
                Seats = (int)enterpriseMonthlyLineItem.Quantity
            });
        }
        else if (organizationId.HasValue)
        {
            if (!subscription.Items.Any(i =>
                    StaticStore.Plans.Any(p => p.PasswordManager.StripePlanId == i.Plan.Id)))
            {
                return;
            }

            await _organizationService.EnableAsync(organizationId.Value, subscription.CurrentPeriodEnd);
            var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);

            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.Rebilled, organization, _currentContext)
                {
                    PlanName = organization?.Plan,
                    PlanType = organization?.PlanType,
                    Seats = organization?.Seats,
                    Storage = organization?.MaxStorageGb,
                });
        }
        else if (userId.HasValue)
        {
            if (subscription.Items.All(i => i.Plan.Id != IStripeEventUtilityService.PremiumPlanId))
            {
                return;
            }

            await _userService.EnablePremiumAsync(userId.Value, subscription.CurrentPeriodEnd);

            var user = await _userRepository.GetByIdAsync(userId.Value);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.Rebilled, user, _currentContext)
                {
                    PlanName = IStripeEventUtilityService.PremiumPlanId,
                    Storage = user?.MaxStorageGb,
                });
        }
    }
}
