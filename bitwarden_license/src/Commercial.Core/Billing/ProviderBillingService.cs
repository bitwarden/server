using System.Globalization;
using Bit.Commercial.Core.Billing.Models;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Contracts;
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
    IGlobalSettings globalSettings,
    ILogger<ProviderBillingService> logger,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService,
    IProviderInvoiceItemRepository providerInvoiceItemRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IProviderBillingService
{
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

        Subscription subscription;
        try
        {
            subscription = await stripeAdapter.ProviderSubscriptionGetAsync(command.GatewaySubscriptionId, plan.ProviderId);
        }
        catch (InvalidOperationException)
        {
            throw new ConflictException("Subscription not found.");
        }

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

        // Refactor later to ?ChangeClientPlanCommand? (ProviderPlanId, ProviderId, OrganizationId)
        // 1. Retrieve PlanType and PlanName for ProviderPlan
        // 2. Assign PlanType & PlanName to Organization
        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(plan.ProviderId);

        foreach (var providerOrganization in providerOrganizations)
        {
            var organization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);
            if (organization == null)
            {
                throw new ConflictException($"Organization '{providerOrganization.Id}' not found.");
            }
            organization.PlanType = command.NewPlan;
            organization.Plan = StaticStore.GetPlan(command.NewPlan).Name;
            await organizationRepository.ReplaceAsync(organization);
        }
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

    public async Task ScaleSeats(
        Provider provider,
        PlanType planType,
        int seatAdjustment)
    {
        var providerPlan = await GetProviderPlanAsync(provider, planType);

        var seatMinimum = providerPlan.SeatMinimum ?? 0;

        var currentlyAssignedSeatTotal = await GetAssignedSeatTotalAsync(provider, planType);

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

    public async Task<bool> SeatAdjustmentResultsInPurchase(
        Provider provider,
        PlanType planType,
        int seatAdjustment)
    {
        var providerPlan = await GetProviderPlanAsync(provider, planType);

        var seatMinimum = providerPlan.SeatMinimum;

        var currentlyAssignedSeatTotal = await GetAssignedSeatTotalAsync(provider, planType);

        var newlyAssignedSeatTotal = currentlyAssignedSeatTotal + seatAdjustment;

        return
            // Below the limit to above the limit
            (currentlyAssignedSeatTotal <= seatMinimum && newlyAssignedSeatTotal > seatMinimum) ||
            // Above the limit to further above the limit
            (currentlyAssignedSeatTotal > seatMinimum && newlyAssignedSeatTotal > seatMinimum && newlyAssignedSeatTotal > currentlyAssignedSeatTotal);
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

    public async Task UpdateSeatMinimums(UpdateProviderSeatMinimumsCommand command)
    {
        if (command.Configuration.Any(x => x.SeatsMinimum < 0))
        {
            throw new BadRequestException("Provider seat minimums must be at least 0.");
        }

        Subscription subscription;
        try
        {
            subscription = await stripeAdapter.ProviderSubscriptionGetAsync(command.GatewaySubscriptionId, command.Id);
        }
        catch (InvalidOperationException)
        {
            throw new ConflictException("Subscription not found.");
        }

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>();

        var providerPlans = await providerPlanRepository.GetByProviderId(command.Id);

        foreach (var newPlanConfiguration in command.Configuration)
        {
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

    // TODO: Replace with SPROC
    private async Task<int> GetAssignedSeatTotalAsync(Provider provider, PlanType planType)
    {
        var providerOrganizations =
            await providerOrganizationRepository.GetManyDetailsByProviderAsync(provider.Id);

        var plan = StaticStore.GetPlan(planType);

        return providerOrganizations
            .Where(providerOrganization => providerOrganization.Plan == plan.Name && providerOrganization.Status == OrganizationStatusType.Managed)
            .Sum(providerOrganization => providerOrganization.Seats ?? 0);
    }

    // TODO: Replace with SPROC
    private async Task<ProviderPlan> GetProviderPlanAsync(Provider provider, PlanType planType)
    {
        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var providerPlan = providerPlans.FirstOrDefault(x => x.PlanType == planType);

        if (providerPlan == null || !providerPlan.IsConfigured())
        {
            throw new BillingException(message: "Provider plan is missing or misconfigured");
        }

        return providerPlan;
    }
}
