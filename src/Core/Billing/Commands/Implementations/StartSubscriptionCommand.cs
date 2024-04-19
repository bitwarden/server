using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Billing.Commands.Implementations;

public class StartSubscriptionCommand(
    IGlobalSettings globalSettings,
    ILogger<StartSubscriptionCommand> logger,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter) : IStartSubscriptionCommand
{
    public async Task StartSubscription(
        Provider provider,
        TaxInfo taxInfo)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(taxInfo);

        if (!string.IsNullOrEmpty(provider.GatewaySubscriptionId))
        {
            logger.LogWarning("Cannot start Provider subscription - Provider ({ID}) already has a {FieldName}", provider.Id, nameof(provider.GatewaySubscriptionId));

            throw ContactSupport();
        }

        if (string.IsNullOrEmpty(taxInfo.BillingAddressCountry) ||
            string.IsNullOrEmpty(taxInfo.BillingAddressPostalCode))
        {
            logger.LogError("Cannot start Provider subscription - Both the Provider's ({ID}) country and postal code are required", provider.Id);

            throw ContactSupport();
        }

        var customer = await GetOrCreateCustomerAsync(provider, taxInfo);

        if (taxInfo.BillingAddressCountry == "US" && customer.Tax is not { AutomaticTax: StripeConstants.AutomaticTaxStatus.Supported })
        {
            logger.LogError("Cannot start Provider subscription - Provider's ({ProviderID}) Stripe customer ({CustomerID}) is in the US and does not support automatic tax", provider.Id, customer.Id);

            throw ContactSupport();
        }

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

    // ReSharper disable once SuggestBaseTypeForParameter
    private async Task<Customer> GetOrCreateCustomerAsync(
        Provider provider,
        TaxInfo taxInfo)
    {
        if (!string.IsNullOrEmpty(provider.GatewayCustomerId))
        {
            var existingCustomer = await stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId, new CustomerGetOptions
            {
                Expand = ["tax"]
            });

            if (existingCustomer != null)
            {
                return existingCustomer;
            }

            logger.LogError("Cannot start Provider subscription - Provider's ({ProviderID}) {CustomerIDFieldName} did not relate to a Stripe customer", provider.Id, nameof(provider.GatewayCustomerId));

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
            Expand = ["tax"],
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

        var createdCustomer = await stripeAdapter.CustomerCreateAsync(customerCreateOptions);

        provider.GatewayCustomerId = createdCustomer.Id;

        await providerRepository.ReplaceAsync(provider);

        return createdCustomer;
    }
}
