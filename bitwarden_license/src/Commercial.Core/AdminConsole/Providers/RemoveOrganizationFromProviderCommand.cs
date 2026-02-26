// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;

namespace Bit.Commercial.Core.AdminConsole.Providers;

public class RemoveOrganizationFromProviderCommand : IRemoveOrganizationFromProviderCommand
{
    private readonly IEventService _eventService;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IFeatureService _featureService;
    private readonly IProviderBillingService _providerBillingService;
    private readonly ISubscriberService _subscriberService;
    private readonly IHasConfirmedOwnersExceptQuery _hasConfirmedOwnersExceptQuery;
    private readonly IPricingClient _pricingClient;

    public RemoveOrganizationFromProviderCommand(
        IEventService eventService,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IStripeAdapter stripeAdapter,
        IFeatureService featureService,
        IProviderBillingService providerBillingService,
        ISubscriberService subscriberService,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
        IPricingClient pricingClient)
    {
        _eventService = eventService;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _stripeAdapter = stripeAdapter;
        _featureService = featureService;
        _providerBillingService = providerBillingService;
        _subscriberService = subscriberService;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
        _pricingClient = pricingClient;
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

        if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                [],
                includeProvider: false))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        var organizationOwnerEmails =
            (await _organizationRepository.GetOwnerEmailAddressesById(organization.Id)).ToList();

        organization.BillingEmail = organizationOwnerEmails.MinBy(email => email);

        await ResetOrganizationBillingAsync(organization, provider, organizationOwnerEmails);

        await _organizationRepository.ReplaceAsync(organization);

        await _providerOrganizationRepository.DeleteAsync(providerOrganization);

        await _eventService.LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Removed);
    }

    /// <summary>
    /// When a client organization is unlinked from a provider, we have to check if they're Stripe-enabled
    /// and, if they are, we remove their MSP discount and set their Subscription to `send_invoice`. This is because
    /// the provider's payment method will be removed from their Stripe customer, causing ensuing charges to fail. Lastly,
    /// we email the organization owners letting them know they need to add a new payment method.
    /// </summary>
    private async Task ResetOrganizationBillingAsync(
        Organization organization,
        Provider provider,
        IEnumerable<string> organizationOwnerEmails)
    {
        if (provider.IsBillable() &&
            organization.IsValidClient())
        {
            // An organization converted to a business unit will not have a Customer since it was given to the business unit.
            if (string.IsNullOrEmpty(organization.GatewayCustomerId))
            {
                await _providerBillingService.CreateCustomerForClientOrganization(provider, organization);
            }

            var customer = await _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, new CustomerUpdateOptions
            {
                Description = string.Empty,
                Email = organization.BillingEmail,
                Expand = ["tax", "tax_ids"]
            });

            var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);

            var subscriptionCreateOptions = new SubscriptionCreateOptions
            {
                Customer = organization.GatewayCustomerId,
                CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
                DaysUntilDue = 30,
                Metadata = new Dictionary<string, string>
                {
                    { "organizationId", organization.Id.ToString() }
                },
                OffSession = true,
                ProrationBehavior = StripeConstants.ProrationBehavior.CreateProrations,
                Items = [new SubscriptionItemOptions { Price = plan.PasswordManager.StripeSeatPlanId, Quantity = organization.Seats }]
            };

            subscriptionCreateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };

            var subscription = await _stripeAdapter.CreateSubscriptionAsync(subscriptionCreateOptions);

            organization.GatewaySubscriptionId = subscription.Id;
            organization.Status = OrganizationStatusType.Created;
            organization.Enabled = true;

            await _providerBillingService.ScaleSeats(provider, organization.PlanType, -organization.Seats ?? 0);
        }
        else if (organization.IsStripeEnabled())
        {
            var subscription = await _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, new SubscriptionGetOptions
            {
                Expand = ["customer"]
            });
            if (subscription.Status is StripeConstants.SubscriptionStatus.Canceled or StripeConstants.SubscriptionStatus.IncompleteExpired)
            {
                return;
            }

            await _stripeAdapter.UpdateCustomerAsync(subscription.CustomerId, new CustomerUpdateOptions
            {
                Email = organization.BillingEmail
            });

            if (subscription.Customer.Discount?.Coupon != null)
            {
                await _stripeAdapter.DeleteCustomerDiscountAsync(subscription.CustomerId);
            }

            await _stripeAdapter.UpdateSubscriptionAsync(organization.GatewaySubscriptionId, new SubscriptionUpdateOptions
            {
                CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
                DaysUntilDue = 30,
            });

            await _subscriberService.RemovePaymentSource(organization);
        }

        await _mailService.SendProviderUpdatePaymentMethod(
            organization.Id,
            organization.Name,
            provider.Name!,
            organizationOwnerEmails);
    }
}
