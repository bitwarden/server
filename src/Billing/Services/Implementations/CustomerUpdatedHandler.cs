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
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
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
        if (parsedEvent == null)
        {
            throw new ArgumentNullException(nameof(parsedEvent));
        }

        if (_stripeEventService == null)
        {
            throw new InvalidOperationException($"{nameof(_stripeEventService)} is not initialized");
        }

        var customer = await _stripeEventService.GetCustomer(parsedEvent, true, ["subscriptions"]);
        if (customer?.Subscriptions == null || !customer.Subscriptions.Any())
        {
            return;
        }

        var subscription = customer.Subscriptions.First();

        if (subscription.Metadata == null)
        {
            return;
        }

        if (_stripeEventUtilityService == null)
        {
            throw new InvalidOperationException($"{nameof(_stripeEventUtilityService)} is not initialized");
        }

        var (organizationId, _, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (!organizationId.HasValue)
        {
            return;
        }

        if (_organizationRepository == null)
        {
            throw new InvalidOperationException($"{nameof(_organizationRepository)} is not initialized");
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);

        if (organization == null)
        {
            return;
        }

        organization.BillingEmail = customer.Email;
        await _organizationRepository.ReplaceAsync(organization);

        if (_referenceEventService == null)
        {
            throw new InvalidOperationException($"{nameof(_referenceEventService)} is not initialized");
        }

        if (_currentContext == null)
        {
            throw new InvalidOperationException($"{nameof(_currentContext)} is not initialized");
        }

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.OrganizationEditedInStripe, organization, _currentContext));
    }
}
