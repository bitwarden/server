using Bit.Billing.Constants;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class CustomerUpdatedHandler : IWebhookEventHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IWebhookUtility _webhookUtility;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;

    public CustomerUpdatedHandler(IStripeEventService stripeEventService,
        IWebhookUtility webhookUtility,
        IOrganizationRepository organizationRepository,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext)
    {
        _stripeEventService = stripeEventService;
        _webhookUtility = webhookUtility;
        _organizationRepository = organizationRepository;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
    }

    public bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.InvoiceCreated);
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var customer = await _stripeEventService.GetCustomer(parsedEvent, true, new List<string> { "subscriptions" });

        if (customer.Subscriptions == null || !customer.Subscriptions.Any())
        {
            return;
        }

        var subscription = customer.Subscriptions.First();

        var (organizationId, _) = _webhookUtility.GetIdsFromMetaData(subscription.Metadata);

        if (!organizationId.HasValue)
        {
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);
        organization.BillingEmail = customer.Email;
        await _organizationRepository.ReplaceAsync(organization);

        await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.OrganizationEditedInStripe, organization, _currentContext));
    }
}
