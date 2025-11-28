using Bit.Core.Repositories;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class CustomerUpdatedHandler : ICustomerUpdatedHandler
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly ILogger<CustomerUpdatedHandler> _logger;

    public CustomerUpdatedHandler(
        IOrganizationRepository organizationRepository,
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService,
        ILogger<CustomerUpdatedHandler> logger)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.CustomerUpdated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        if (parsedEvent == null)
        {
            _logger.LogError("Parsed event was null in CustomerUpdatedHandler");
            throw new ArgumentNullException(nameof(parsedEvent));
        }

        if (_stripeEventService == null)
        {
            _logger.LogError("StripeEventService was not initialized in CustomerUpdatedHandler");
            throw new InvalidOperationException($"{nameof(_stripeEventService)} is not initialized");
        }

        var customer = await _stripeEventService.GetCustomer(parsedEvent, true, ["subscriptions"]);
        if (customer?.Subscriptions == null || !customer.Subscriptions.Any())
        {
            _logger.LogWarning("Customer or subscriptions were null or empty in CustomerUpdatedHandler. Customer ID: {CustomerId}", customer?.Id);
            return;
        }

        var subscription = customer.Subscriptions.First();

        if (subscription.Metadata == null)
        {
            _logger.LogWarning("Subscription metadata was null in CustomerUpdatedHandler. Subscription ID: {SubscriptionId}", subscription.Id);
            return;
        }

        if (_stripeEventUtilityService == null)
        {
            _logger.LogError("StripeEventUtilityService was not initialized in CustomerUpdatedHandler");
            throw new InvalidOperationException($"{nameof(_stripeEventUtilityService)} is not initialized");
        }

        var (organizationId, _, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (!organizationId.HasValue)
        {
            _logger.LogWarning("Organization ID was not found in subscription metadata. Subscription ID: {SubscriptionId}", subscription.Id);
            return;
        }

        if (_organizationRepository == null)
        {
            _logger.LogError("OrganizationRepository was not initialized in CustomerUpdatedHandler");
            throw new InvalidOperationException($"{nameof(_organizationRepository)} is not initialized");
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);

        if (organization == null)
        {
            _logger.LogWarning("Organization not found. Organization ID: {OrganizationId}", organizationId.Value);
            return;
        }

        organization.BillingEmail = customer.Email;
        await _organizationRepository.ReplaceAsync(organization);
    }
}
