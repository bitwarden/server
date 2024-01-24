using Bit.Billing.Constants;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentSucceededHandler : IWebhookEventHandler
{
    private const string _premiumPlanId = "premium-annually";

    private readonly IStripeEventService _stripeEventService;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IWebhookUtility _webhookUtility;

    public PaymentSucceededHandler(IStripeEventService stripeEventService,
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext,
        IUserService userService,
        IUserRepository userRepository,
        IWebhookUtility webhookUtility)
    {
        _stripeEventService = stripeEventService;
        _organizationService = organizationService;
        _organizationRepository = organizationRepository;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _userService = userService;
        _userRepository = userRepository;
        _webhookUtility = webhookUtility;
    }
    public bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.PaymentSucceeded);
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (invoice.Paid && invoice.BillingReason == "subscription_create")
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
            if (subscription?.Status == StripeSubscriptionStatus.Active)
            {
                if (DateTime.UtcNow - invoice.Created < TimeSpan.FromMinutes(1))
                {
                    await Task.Delay(5000);
                }

                var ids = _webhookUtility.GetIdsFromMetaData(subscription.Metadata);
                // org
                if (ids.Item1.HasValue)
                {
                    if (subscription.Items.Any(i =>
                            StaticStore.Plans.Any(p => p.PasswordManager.StripePlanId == i.Plan.Id)))
                    {
                        await _organizationService.EnableAsync(ids.Item1.Value, subscription.CurrentPeriodEnd);

                        var organization = await _organizationRepository.GetByIdAsync(ids.Item1.Value);
                        await _referenceEventService.RaiseEventAsync(
                            new ReferenceEvent(ReferenceEventType.Rebilled, organization, _currentContext)
                            {
                                PlanName = organization?.Plan,
                                PlanType = organization?.PlanType,
                                Seats = organization?.Seats,
                                Storage = organization?.MaxStorageGb,
                            });
                    }
                }
                // user
                else if (ids.Item2.HasValue)
                {
                    if (subscription.Items.Any(i => i.Plan.Id == _premiumPlanId))
                    {
                        await _userService.EnablePremiumAsync(ids.Item2.Value, subscription.CurrentPeriodEnd);

                        var user = await _userRepository.GetByIdAsync(ids.Item2.Value);
                        await _referenceEventService.RaiseEventAsync(
                            new ReferenceEvent(ReferenceEventType.Rebilled, user, _currentContext)
                            {
                                PlanName = _premiumPlanId,
                                Storage = user?.MaxStorageGb,
                            });
                    }
                }
            }
        }
    }
}
