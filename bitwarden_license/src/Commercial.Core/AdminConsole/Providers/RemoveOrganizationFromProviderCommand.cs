using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Commercial.Core.AdminConsole.Providers;

public class RemoveOrganizationFromProviderCommand : IRemoveOrganizationFromProviderCommand
{
    private readonly IEventService _eventService;
    private readonly ILogger<RemoveOrganizationFromProviderCommand> _logger;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IScaleSeatsCommand _scaleSeatsCommand;
    private readonly IFeatureService _featureService;

    public RemoveOrganizationFromProviderCommand(
        IEventService eventService,
        ILogger<RemoveOrganizationFromProviderCommand> logger,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IProviderOrganizationRepository providerOrganizationRepository,
        IStripeAdapter stripeAdapter,
        IScaleSeatsCommand scaleSeatsCommand,
        IFeatureService featureService)
    {
        _eventService = eventService;
        _logger = logger;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _providerOrganizationRepository = providerOrganizationRepository;
        _stripeAdapter = stripeAdapter;
        _scaleSeatsCommand = scaleSeatsCommand;
        _featureService = featureService;
    }

    public async Task RemoveOrganizationFromProvider(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization)
    {
        if (provider == null ||
            providerOrganization == null ||
            organization == null ||
            providerOrganization.ProviderId != provider.Id)
        {
            throw new BadRequestException("Failed to remove organization. Please contact support.");
        }

        if (!await _organizationService.HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                Array.Empty<Guid>(),
                includeProvider: false))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        var organizationOwnerEmails =
            (await _organizationRepository.GetOwnerEmailAddressesById(organization.Id)).ToList();

        organization.BillingEmail = organizationOwnerEmails.MinBy(email => email);

        var customerUpdateOptions = new CustomerUpdateOptions
        {
            Coupon = string.Empty,
            Email = organization.BillingEmail
        };

        await _stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId, customerUpdateOptions);

        var isConsolidatedBillingEnabled = _featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling);

        if (isConsolidatedBillingEnabled && provider.Status == ProviderStatusType.Billable)
        {
            var plan = StaticStore.GetPlan(organization.PlanType).PasswordManager;
            var subscription = await _stripeAdapter.SubscriptionCreateAsync(new SubscriptionCreateOptions
            {
                Customer = organization.GatewayCustomerId,
                CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
                DaysUntilDue = 30,
                AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
                Metadata = new Dictionary<string, string>
                {
                    { "organizationId", organization.Id.ToString() }
                },
                OffSession = true,
                ProrationBehavior = StripeConstants.ProrationBehavior.CreateProrations,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions { Price = plan.StripeSeatPlanId, Quantity = organization.Seats }
                }
            });
            organization.GatewaySubscriptionId = subscription.Id;
            await _scaleSeatsCommand.ScalePasswordManagerSeats(provider, organization.PlanType,
                -(organization.Seats ?? 0));
        }
        else
        {
            var subscriptionUpdateOptions = new SubscriptionUpdateOptions
            {
                CollectionMethod = "send_invoice",
                DaysUntilDue = 30
            };
            await _stripeAdapter.SubscriptionUpdateAsync(organization.GatewaySubscriptionId, subscriptionUpdateOptions);
        }

        await _organizationRepository.ReplaceAsync(organization);

        await _mailService.SendProviderUpdatePaymentMethod(
            organization.Id,
            organization.Name,
            provider.Name,
            organizationOwnerEmails);

        await _providerOrganizationRepository.DeleteAsync(providerOrganization);

        await _eventService.LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Removed);
    }
}
