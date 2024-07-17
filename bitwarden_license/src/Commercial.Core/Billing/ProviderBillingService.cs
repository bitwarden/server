using System.Globalization;
using Bit.Commercial.Core.Billing.Models;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Utilities;

namespace Bit.Commercial.Core.Billing;

public class ProviderBillingService(
    ICurrentContext currentContext,
    IGlobalSettings globalSettings,
    ILogger<ProviderBillingService> logger,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IPaymentService paymentService,
    IProviderInvoiceItemRepository providerInvoiceItemRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IProviderBillingService
{
    public async Task AssignSeatsToClientOrganization(
        Provider provider,
        Organization organization,
        int seats)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(organization);

        if (seats < 0)
        {
            throw new BillingException("You cannot assign negative seats to a client.");
        }

        if (seats == organization.Seats)
        {
            logger.LogWarning("Client organization ({ID}) already has {Seats} seats assigned to it", organization.Id, organization.Seats);

            return;
        }

        var organizationUsers = await organizationUserRepository.GetManyDetailsByOrganizationAsync(organization.Id);

        if (seats < organizationUsers.Count)
        {
            throw new BillingException("You cannot assign a client less seats than the number of members they have.");
        }

        var seatAdjustment = seats - (organization.Seats ?? 0);

        await ScaleSeats(provider, organization.PlanType, seatAdjustment);

        organization.Seats = seats;

        await organizationRepository.ReplaceAsync(organization);
    }

    public async Task<Customer> CreateCustomerForSetup(
        Provider provider,
        TaxInfo taxInfo)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(taxInfo);

        if (string.IsNullOrEmpty(taxInfo.BillingAddressCountry) ||
            string.IsNullOrEmpty(taxInfo.BillingAddressPostalCode))
        {
            logger.LogError("Cannot create customer for provider ({ProviderID}) without both a country and postal code", provider.Id);

            throw new BillingException("Both address and postal code are required to set up your provider.");
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
            Description = provider.DisplayBusinessName(),
            Email = provider.BillingEmail,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = provider.SubscriberType(),
                        Value = providerDisplayName?.Length <= 30
                            ? providerDisplayName
                            : providerDisplayName?[..30]
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

        try
        {
            return await stripeAdapter.CustomerCreateAsync(customerCreateOptions);
        }
        catch (StripeException exception)
        {
            logger.LogError("An error occurred while trying to create a Stripe customer for provider ({ProviderID}): {Error}",
                provider.Id,
                exception.GetErrorMessage());

            throw new BillingException(
                "We had a problem setting up your provider. Please contact support.",
                "An error occurred while trying to create a provider's Stripe customer",
                exception);
        }
    }

    public async Task<Customer> CreateCustomerForClientOrganization(
        Provider provider,
        Organization organization)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(organization);

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

        try
        {
            return await stripeAdapter.CustomerCreateAsync(customerCreateOptions);
        }
        catch (StripeException exception)
        {
            logger.LogError("An error occurred while trying to create a Stripe customer for provider's ({ProviderID}) client organization ({OrganizationID}): {Error}",
                provider.Id,
                organization.Id,
                exception.GetErrorMessage());

            throw new BillingException(
                "We had a problem creating your client organization. Please contact support.",
                "An error occurred while trying to create a client organization's Stripe customer",
                exception);
        }
    }

    public async Task<byte[]> GenerateClientInvoiceReport(
        string invoiceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(invoiceId);

        var invoiceItems = await providerInvoiceItemRepository.GetByInvoiceId(invoiceId);

        if (invoiceItems.Count == 0)
        {
            logger.LogError("Could not find invoice items for invoice ({InvoiceID}) when generating client invoice report", invoiceId);

            throw new BillingException("We had a problem generating your invoice report. Please contact support.");
        }

        var csvRows = invoiceItems.Select(ProviderClientInvoiceReportRow.From);

        using var memoryStream = new MemoryStream();

        await using var streamWriter = new StreamWriter(memoryStream);

        await using var csvWriter = new CsvWriter(streamWriter, CultureInfo.CurrentCulture);

        await csvWriter.WriteRecordsAsync(csvRows);

        await streamWriter.FlushAsync();

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream.ToArray();
    }

    public async Task<ConsolidatedBillingSubscriptionDTO> GetConsolidatedBillingSubscription(
        Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var subscription = await subscriberService.GetSubscription(provider, new SubscriptionGetOptions
        {
            Expand = ["customer", "test_clock"]
        });

        if (subscription == null)
        {
            return null;
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var configuredProviderPlans = providerPlans
            .Where(providerPlan => providerPlan.IsConfigured())
            .Select(ConfiguredProviderPlanDTO.From)
            .ToList();

        var taxInformation = await subscriberService.GetTaxInformation(provider);

        var suspension = await GetSuspensionAsync(stripeAdapter, subscription);

        return new ConsolidatedBillingSubscriptionDTO(
            configuredProviderPlans,
            subscription,
            taxInformation,
            suspension);
    }

    public async Task ScaleSeats(
        Provider provider,
        PlanType planType,
        int seatAdjustment)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!planType.SupportsConsolidatedBilling())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) seats for plan type {PlanType} as it does not support consolidated billing", provider.Id, planType.ToString());

            throw new BillingException();
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var providerPlan = providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == planType);

        if (providerPlan == null || !providerPlan.IsConfigured())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) seats for plan type {PlanType} when their matching provider plan is not configured", provider.Id, planType);

            throw new BillingException();
        }

        var seatMinimum = providerPlan.SeatMinimum.GetValueOrDefault(0);

        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(provider.Id);

        var plan = StaticStore.GetPlan(planType);

        var currentlyAssignedSeatTotal = providerOrganizations
            .Where(providerOrganization => providerOrganization.Plan == plan.Name && providerOrganization.Status == OrganizationStatusType.Managed)
            .Sum(providerOrganization => providerOrganization.Seats ?? 0);

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
            if (!currentContext.ProviderProviderAdmin(provider.Id))
            {
                logger.LogError("Service user for provider ({ProviderID}) cannot scale a provider's seat count over the seat minimum", provider.Id);

                throw new BillingException("Service users do not have permission to purchase seats.");
            }

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

    public async Task<Subscription> StartSubscriptionForSetup(
        Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var customer = await subscriberService.GetCustomerOrThrow(provider);

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        if (providerPlans == null || providerPlans.Count == 0)
        {
            logger.LogError("Cannot start subscription for provider ({ProviderID}) that has no configured plans", provider.Id);

            throw new BillingException();
        }

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>();

        var teamsProviderPlan =
            providerPlans.SingleOrDefault(providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly);

        if (teamsProviderPlan == null || !teamsProviderPlan.IsConfigured())
        {
            logger.LogError("Cannot start subscription for provider ({ProviderID}) that has no configured Teams plan", provider.Id);

            throw new BillingException();
        }

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
        {
            Price = teamsPlan.PasswordManager.StripeProviderPortalSeatPlanId,
            Quantity = teamsProviderPlan.SeatMinimum
        });

        var enterpriseProviderPlan =
            providerPlans.SingleOrDefault(providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly);

        if (enterpriseProviderPlan == null || !enterpriseProviderPlan.IsConfigured())
        {
            logger.LogError("Cannot start subscription for provider ({ProviderID}) that has no configured Enterprise plan", provider.Id);

            throw new BillingException();
        }

        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
        {
            Price = enterprisePlan.PasswordManager.StripeProviderPortalSeatPlanId,
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

        try
        {
            var subscription = await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);

            if (subscription.Status == StripeConstants.SubscriptionStatus.Active)
            {
                return subscription;
            }

            logger.LogError(
                "Provider's ({ProviderID}) newly created subscription ({SubscriptionID}) has incorrect status: {Status}",
                provider.Id, subscription.Id, subscription.Status);

            throw new BillingException();
        }
        catch (StripeException exception) when (exception.StripeError?.Code ==
                                                StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException("Your location wasn't recognized. Please ensure your country and postal code are valid.");
        }
        catch (StripeException exception)
        {
            logger.LogError("An error occurred while trying to create a Stripe subscription for provider ({ProviderID}): {Error}",
                provider.Id,
                exception.GetErrorMessage());

            throw new BillingException(
                "We had a problem setting up your provider. Please contact support.",
                "An error occurred while trying to create a provider's Stripe subscription",
                exception);
        }
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
