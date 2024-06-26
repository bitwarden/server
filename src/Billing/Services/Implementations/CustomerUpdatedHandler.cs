using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class CustomerUpdatedHandler : ICustomerUpdatedHandler
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;

    public CustomerUpdatedHandler(
        IOrganizationRepository organizationRepository,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext,
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService)
    {
        _organizationRepository = organizationRepository;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.CustomerUpdated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var customer = await _stripeEventService.GetCustomer(parsedEvent, true, ["subscriptions"]);
        if (customer.Subscriptions == null || !customer.Subscriptions.Any())
        {
            return;
        }

        var subscription = customer.Subscriptions.First();

        var (organizationId, _, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (!organizationId.HasValue)
        {
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);
        organization.BillingEmail = customer.Email;
        await _organizationRepository.ReplaceAsync(organization);

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.OrganizationEditedInStripe, organization, _currentContext));
    }
}
