using System.Globalization;
using Bit.Commercial.Core.Billing.Models;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Contracts;
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

namespace Bit.Commercial.Core.Billing;

public class ProviderBillingService(
    ICurrentContext currentContext,
    IGlobalSettings globalSettings,
    ILogger<ProviderBillingService> logger,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService,
    IProviderInvoiceItemRepository providerInvoiceItemRepository,
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

    public async Task<byte[]> GenerateClientInvoiceReport(
        string invoiceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(invoiceId);

        var invoiceItems = await providerInvoiceItemRepository.GetByInvoiceId(invoiceId);

        if (invoiceItems.Count == 0)
        {
            logger.LogError("No provider invoice item records were found for invoice ({InvoiceID})", invoiceId);

            return null;
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

            throw new BillingException();
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Assigned seats cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw new BillingException();
        }

        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId);

        var plan = StaticStore.GetPlan(planType);

        return providerOrganizations
            .Where(providerOrganization => providerOrganization.Plan == plan.Name && providerOrganization.Status == OrganizationStatusType.Managed)
            .Sum(providerOrganization => providerOrganization.Seats ?? 0);
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

            throw new BillingException();
        }

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
            if (!currentContext.ProviderProviderAdmin(provider.Id))
            {
                logger.LogError("Service user for provider ({ProviderID}) cannot scale a provider's seat count over the seat minimum", provider.Id);

                throw new BillingException();
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

    public async Task<Customer> SetupCustomer(
        Provider provider,
        TaxInfo taxInfo)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(taxInfo);

        if (string.IsNullOrEmpty(taxInfo.BillingAddressCountry) ||
            string.IsNullOrEmpty(taxInfo.BillingAddressPostalCode))
        {
            logger.LogError("Cannot create customer for provider ({ProviderID}) without both a country and postal code", provider.Id);

            throw new BillingException();
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
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.TaxIdInvalid)
        {
            throw new BadRequestException("Your tax ID wasn't recognized for your selected country. Please ensure your country and tax ID are valid.");
        }
    }

    public async Task<Subscription> SetupSubscription(
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

        foreach (var providerPlan in providerPlans)
        {
            var plan = StaticStore.GetPlan(providerPlan.PlanType);

            if (!providerPlan.IsConfigured())
            {
                logger.LogError("Cannot start subscription for provider ({ProviderID}) that has no configured {ProviderName} plan", provider.Id, plan.Name);
                throw new BillingException();
            }

            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = plan.PasswordManager.StripeProviderPortalSeatPlanId,
                Quantity = providerPlan.SeatMinimum
            });
        }

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
                "Newly created provider ({ProviderID}) subscription ({SubscriptionID}) has inactive status: {Status}",
                provider.Id,
                subscription.Id,
                subscription.Status);

            throw new BillingException();
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException("Your location wasn't recognized. Please ensure your country and postal code are valid.");
        }
    }

    public async Task ChangePlan(ChangeProviderPlanCommand command)
    {
        var plan = await providerPlanRepository.GetByIdAsync(command.ProviderPlanId);

        if (plan == null)
        {
            throw new BadRequestException("Provider plan not found.");
        }

        if (plan.PlanType == command.NewPlan)
        {
            return;
        }

        var oldPlanConfiguration = StaticStore.GetPlan(plan.PlanType);

        plan.PlanType = command.NewPlan;
        await providerPlanRepository.ReplaceAsync(plan);

        var subscription = await stripeAdapter.SubscriptionGetAsync(command.GatewaySubscriptionId);

        var oldSubscriptionItem = subscription.Items.SingleOrDefault(x =>
            x.Price.Id == oldPlanConfiguration.PasswordManager.StripeProviderPortalSeatPlanId);

        var updateOptions = new SubscriptionUpdateOptions
        {
            Items =
            [
                new SubscriptionItemOptions
                {
                    Price = StaticStore.GetPlan(command.NewPlan).PasswordManager.StripeProviderPortalSeatPlanId,
                    Quantity = oldSubscriptionItem!.Quantity
                },
                new SubscriptionItemOptions
                {
                    Id = oldSubscriptionItem.Id,
                    Deleted = true
                }
            ]
        };

        await stripeAdapter.SubscriptionUpdateAsync(command.GatewaySubscriptionId, updateOptions);
    }

    public async Task UpdateSeatMinimums(UpdateProviderSeatMinimumsCommand command)
    {
        var subscription = await stripeAdapter.SubscriptionGetAsync(command.GatewaySubscriptionId);

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>();

        var providerPlans = await providerPlanRepository.GetByProviderId(command.Id);

        foreach (var newPlanConfiguration in command.Configuration)
        {
            if (newPlanConfiguration.SeatsMinimum < 0)
            {
                throw new BadRequestException("Provider seat minimums must be at least 0.");
            }

            var providerPlan =
                providerPlans.Single(providerPlan => providerPlan.PlanType == newPlanConfiguration.Plan);

            if (providerPlan.SeatMinimum != newPlanConfiguration.SeatsMinimum)
            {
                var priceId = StaticStore.GetPlan(newPlanConfiguration.Plan).PasswordManager
                    .StripeProviderPortalSeatPlanId;
                var subscriptionItem = subscription.Items.First(item => item.Price.Id == priceId);

                if (providerPlan.PurchasedSeats == 0)
                {
                    if (providerPlan.AllocatedSeats > newPlanConfiguration.SeatsMinimum)
                    {
                        providerPlan.PurchasedSeats = providerPlan.AllocatedSeats - newPlanConfiguration.SeatsMinimum;

                        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                        {
                            Id = subscriptionItem.Id,
                            Price = priceId,
                            Quantity = providerPlan.AllocatedSeats
                        });
                    }
                    else
                    {
                        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                        {
                            Id = subscriptionItem.Id,
                            Price = priceId,
                            Quantity = newPlanConfiguration.SeatsMinimum
                        });
                    }
                }
                else
                {
                    var totalSeats = providerPlan.SeatMinimum + providerPlan.PurchasedSeats;

                    if (newPlanConfiguration.SeatsMinimum <= totalSeats)
                    {
                        providerPlan.PurchasedSeats = totalSeats - newPlanConfiguration.SeatsMinimum;
                    }
                    else
                    {
                        providerPlan.PurchasedSeats = 0;
                        subscriptionItemOptionsList.Add(new SubscriptionItemOptions
                        {
                            Id = subscriptionItem.Id,
                            Price = priceId,
                            Quantity = newPlanConfiguration.SeatsMinimum
                        });
                    }
                }

                providerPlan.SeatMinimum = newPlanConfiguration.SeatsMinimum;

                await providerPlanRepository.ReplaceAsync(providerPlan);
            }
        }

        if (subscriptionItemOptionsList.Count > 0)
        {
            await stripeAdapter.SubscriptionUpdateAsync(command.GatewaySubscriptionId,
                new SubscriptionUpdateOptions { Items = subscriptionItemOptionsList });
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
