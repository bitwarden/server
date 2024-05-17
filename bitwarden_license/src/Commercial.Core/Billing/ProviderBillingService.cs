using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Utilities;

namespace Bit.Commercial.Core.Billing;

public class ProviderBillingService(
    IGlobalSettings globalSettings,
    ILogger<ProviderBillingService> logger,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IProviderBillingService
{
    public async Task AssignSeatsToClientOrganization(
        Provider provider,
        Organization organization,
        int seats)
    {
        ArgumentNullException.ThrowIfNull(organization);

        if (seats < 0)
        {
            throw new BillingException(
                "You cannot assign negative seats to a client.",
                "MSP cannot assign negative seats to a client organization");
        }

        if (seats == organization.Seats)
        {
            logger.LogWarning("Client organization ({ID}) already has {Seats} seats assigned to it", organization.Id, organization.Seats);

            return;
        }

        var seatAdjustment = seats - (organization.Seats ?? 0);

        await ScaleSeats(provider, organization.PlanType, seatAdjustment);

        organization.Seats = seats;

        await organizationRepository.ReplaceAsync(organization);
    }

    public async Task CreateCustomer(
        Provider provider,
        TaxInfo taxInfo)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(taxInfo);

        if (string.IsNullOrEmpty(taxInfo.BillingAddressCountry) ||
            string.IsNullOrEmpty(taxInfo.BillingAddressPostalCode))
        {
            logger.LogError("Cannot create Stripe customer for provider ({ID}) - Both the provider's country and postal code are required", provider.Id);

            throw ContactSupport();
        }

        var providerDisplayName = provider.DisplayName();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = new AddressOptions
            {
                Country = taxInfo.BillingAddressCountry,
                PostalCode = taxInfo.BillingAddressPostalCode,
                Line1 = taxInfo.BillingAddressLine1,
                Line2 = taxInfo.BillingAddressLine2,
                City = taxInfo.BillingAddressCity,
                State = taxInfo.BillingAddressState
            },
            Coupon = "msp-discount-35",
            Description = provider.DisplayBusinessName(),
            Email = provider.BillingEmail,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = provider.SubscriberType(),
                        Value = providerDisplayName.Length <= 30
                            ? providerDisplayName
                            : providerDisplayName[..30]
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "region", globalSettings.BaseServiceUri.CloudRegion }
            },
            TaxIdData = taxInfo.HasTaxId ?
                [
                    new CustomerTaxIdDataOptions { Type = taxInfo.TaxIdType, Value = taxInfo.TaxIdNumber }
                ]
                : null
        };

        var customer = await stripeAdapter.CustomerCreateAsync(customerCreateOptions);

        provider.GatewayCustomerId = customer.Id;

        await providerRepository.ReplaceAsync(provider);
    }

    public async Task CreateCustomerForClientOrganization(
        Provider provider,
        Organization organization)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(organization);

        if (!string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            logger.LogWarning("Client organization ({ID}) already has a populated {FieldName}", organization.Id, nameof(organization.GatewayCustomerId));

            return;
        }

        var providerCustomer = await subscriberService.GetCustomerOrThrow(provider, new CustomerGetOptions
        {
            Expand = ["tax_ids"]
        });

        var providerTaxId = providerCustomer.TaxIds.FirstOrDefault();

        var organizationDisplayName = organization.DisplayName();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = new AddressOptions
            {
                Country = providerCustomer.Address?.Country,
                PostalCode = providerCustomer.Address?.PostalCode,
                Line1 = providerCustomer.Address?.Line1,
                Line2 = providerCustomer.Address?.Line2,
                City = providerCustomer.Address?.City,
                State = providerCustomer.Address?.State
            },
            Name = organizationDisplayName,
            Description = $"{provider.Name} Client Organization",
            Email = provider.BillingEmail,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(),
                        Value = organizationDisplayName.Length <= 30
                            ? organizationDisplayName
                            : organizationDisplayName[..30]
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "region", globalSettings.BaseServiceUri.CloudRegion }
            },
            TaxIdData = providerTaxId == null ? null :
            [
                new CustomerTaxIdDataOptions
                {
                    Type = providerTaxId.Type,
                    Value = providerTaxId.Value
                }
            ]
        };

        var customer = await stripeAdapter.CustomerCreateAsync(customerCreateOptions);

        organization.GatewayCustomerId = customer.Id;

        await organizationRepository.ReplaceAsync(organization);
    }

    public async Task<int> GetAssignedSeatTotalForPlanOrThrow(
        Guid providerId,
        PlanType planType)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving assigned seat total",
                providerId);

            throw ContactSupport();
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Assigned seats cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId);

        var plan = StaticStore.GetPlan(planType);

        return providerOrganizations
            .Where(providerOrganization => providerOrganization.Plan == plan.Name && providerOrganization.Status == OrganizationStatusType.Managed)
            .Sum(providerOrganization => providerOrganization.Seats ?? 0);
    }

    public async Task<ProviderSubscriptionDTO> GetSubscriptionDTO(Guid providerId)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving subscription data.",
                providerId);

            return null;
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Subscription data cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        var subscription = await subscriberService.GetSubscription(provider, new SubscriptionGetOptions
        {
            Expand = ["customer"]
        });

        if (subscription == null)
        {
            return null;
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(providerId);

        var configuredProviderPlans = providerPlans
            .Where(providerPlan => providerPlan.IsConfigured())
            .Select(ConfiguredProviderPlanDTO.From)
            .ToList();

        return new ProviderSubscriptionDTO(
            configuredProviderPlans,
            subscription);
    }

    public async Task ScaleSeats(
        Provider provider,
        PlanType planType,
        int seatAdjustment)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Type != ProviderType.Msp)
        {
            logger.LogError("Non-MSP provider ({ProviderID}) cannot scale their seats", provider.Id);

            throw ContactSupport();
        }

        if (!planType.SupportsConsolidatedBilling())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) seats for plan type {PlanType} as it does not support consolidated billing", provider.Id, planType.ToString());

            throw ContactSupport();
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var providerPlan = providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == planType);

        if (providerPlan == null || !providerPlan.IsConfigured())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) seats for plan type {PlanType} when their matching provider plan is not configured", provider.Id, planType);

            throw ContactSupport();
        }

        var seatMinimum = providerPlan.SeatMinimum.GetValueOrDefault(0);

        var currentlyAssignedSeatTotal = await GetAssignedSeatTotalForPlanOrThrow(provider.Id, planType);

        var newlyAssignedSeatTotal = currentlyAssignedSeatTotal + seatAdjustment;

        var update = CurrySeatScalingUpdate(
            provider,
            providerPlan,
            newlyAssignedSeatTotal);

        /*
         * Below the limit => Below the limit:
         * No subscription update required. We can safely update the provider's allocated seats.
         */
        if (currentlyAssignedSeatTotal <= seatMinimum &&
            newlyAssignedSeatTotal <= seatMinimum)
        {
            providerPlan.AllocatedSeats = newlyAssignedSeatTotal;

            await providerPlanRepository.ReplaceAsync(providerPlan);
        }
        /*
         * Below the limit => Above the limit:
         * We have to scale the subscription up from the seat minimum to the newly assigned seat total.
         */
        else if (currentlyAssignedSeatTotal <= seatMinimum &&
                 newlyAssignedSeatTotal > seatMinimum)
        {
            await update(
                seatMinimum,
                newlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Above the limit:
         * We have to scale the subscription from the currently assigned seat total to the newly assigned seat total.
         */
        else if (currentlyAssignedSeatTotal > seatMinimum &&
                 newlyAssignedSeatTotal > seatMinimum)
        {
            await update(
                currentlyAssignedSeatTotal,
                newlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Below the limit:
         * We have to scale the subscription down from the currently assigned seat total to the seat minimum.
         */
        else if (currentlyAssignedSeatTotal > seatMinimum &&
                 newlyAssignedSeatTotal <= seatMinimum)
        {
            await update(
                currentlyAssignedSeatTotal,
                seatMinimum);
        }
    }

    public async Task StartSubscription(
        Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!string.IsNullOrEmpty(provider.GatewaySubscriptionId))
        {
            logger.LogWarning("Cannot start Provider subscription - Provider ({ID}) already has a {FieldName}", provider.Id, nameof(provider.GatewaySubscriptionId));

            throw ContactSupport();
        }

        var customer = await subscriberService.GetCustomerOrThrow(provider);

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        if (providerPlans == null || providerPlans.Count == 0)
        {
            logger.LogError("Cannot start Provider subscription - Provider ({ID}) has no configured plans", provider.Id);

            throw ContactSupport();
        }

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>();

        var teamsProviderPlan =
            providerPlans.SingleOrDefault(providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly);

        if (teamsProviderPlan == null)
        {
            logger.LogError("Cannot start Provider subscription - Provider ({ID}) has no configured Teams Monthly plan", provider.Id);

            throw ContactSupport();
        }

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
        {
            Price = teamsPlan.PasswordManager.StripeSeatPlanId,
            Quantity = teamsProviderPlan.SeatMinimum
        });

        var enterpriseProviderPlan =
            providerPlans.SingleOrDefault(providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly);

        if (enterpriseProviderPlan == null)
        {
            logger.LogError("Cannot start Provider subscription - Provider ({ID}) has no configured Enterprise Monthly plan", provider.Id);

            throw ContactSupport();
        }

        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
        {
            Price = enterprisePlan.PasswordManager.StripeSeatPlanId,
            Quantity = enterpriseProviderPlan.SeatMinimum
        });

        var subscriptionCreateOptions = new SubscriptionCreateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = true
            },
            CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
            Customer = customer.Id,
            DaysUntilDue = 30,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                { "providerId", provider.Id.ToString() }
            },
            OffSession = true,
            ProrationBehavior = StripeConstants.ProrationBehavior.CreateProrations
        };

        var subscription = await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);

        provider.GatewaySubscriptionId = subscription.Id;

        if (subscription.Status == StripeConstants.SubscriptionStatus.Incomplete)
        {
            await providerRepository.ReplaceAsync(provider);

            logger.LogError("Started incomplete Provider ({ProviderID}) subscription ({SubscriptionID})", provider.Id, subscription.Id);

            throw ContactSupport();
        }

        provider.Status = ProviderStatusType.Billable;

        await providerRepository.ReplaceAsync(provider);
    }

    public async Task<ProviderPaymentInfoDTO> GetPaymentInformationAsync(Guid providerId)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving payment information.",
                providerId);

            return null;
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("payment information cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        var taxInformation = await subscriberService.GetTaxInformationAsync(provider);
        var billingInformation = await paymentService.GetBillingAsync(provider);

        if (taxInformation == null && billingInformation == null)
        {
            return null;
        }

        return new ProviderPaymentInfoDTO(
            billingInformation.PaymentSource,
            taxInformation);

    }

    private Func<int, int, Task> CurrySeatScalingUpdate(
        Provider provider,
        ProviderPlan providerPlan,
        int newlyAssignedSeats) => async (currentlySubscribedSeats, newlySubscribedSeats) =>
    {
        var plan = StaticStore.GetPlan(providerPlan.PlanType);

        await paymentService.AdjustSeats(
            provider,
            plan,
            currentlySubscribedSeats,
            newlySubscribedSeats);

        var newlyPurchasedSeats = newlySubscribedSeats > providerPlan.SeatMinimum
            ? newlySubscribedSeats - providerPlan.SeatMinimum
            : 0;

        providerPlan.PurchasedSeats = newlyPurchasedSeats;
        providerPlan.AllocatedSeats = newlyAssignedSeats;

        await providerPlanRepository.ReplaceAsync(providerPlan);
    };
}
