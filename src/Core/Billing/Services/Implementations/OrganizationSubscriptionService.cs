using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Stripe;
using Customer = Stripe.Customer;
using StaticStore = Bit.Core.Models.StaticStore;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Services.Implementations;

public class OrganizationSubscriptionService(
    IGlobalSettings globalSettings,
    ILogger<SubscriberService> logger,
    IStripeAdapter stripeAdapter) : IOrganizationSubscriptionService
{
    private const string SecretsManagerStandaloneDiscountId = "sm-standalone";

    public async Task<string> PurchaseOrganizationNoPaymentMethod(Organization org, StaticStore.Plan plan,
       int additionalSeats, bool premiumAccessAddon, int additionalSmSeats = 0, int additionalServiceAccount = 0,
       bool signupIsFromSecretsManagerTrial = false)
    {
        var stripeCustomerMetadata = new Dictionary<string, string>
        {
            { "region", globalSettings.BaseServiceUri.CloudRegion }
        };
        var subCreateOptions = new OrganizationPurchaseSubscriptionOptions(org, plan, new TaxInfo(), additionalSeats, 0, premiumAccessAddon
        , additionalSmSeats, additionalServiceAccount);

        Customer customer = null;
        Subscription subscription;
        try
        {
            var customerCreateOptions = new CustomerCreateOptions
            {
                Description = org.DisplayBusinessName(),
                Email = org.BillingEmail,
                Metadata = stripeCustomerMetadata,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = org.SubscriberType(),
                            Value = GetFirstThirtyCharacters(org.SubscriberName()),
                        }
                    ],
                },
                Coupon = signupIsFromSecretsManagerTrial
                    ? SecretsManagerStandaloneDiscountId
                    : null,
                TaxIdData = null,
            };

            customer = await stripeAdapter.CustomerCreateAsync(customerCreateOptions);
            subCreateOptions.AddExpand("latest_invoice.payment_intent");
            subCreateOptions.Customer = customer.Id;

            subscription = await stripeAdapter.SubscriptionCreateAsync(subCreateOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating customer, walking back operation.");
            if (customer != null)
            {
                await stripeAdapter.CustomerDeleteAsync(customer.Id);
            }

            throw;
        }

        org.Gateway = GatewayType.Stripe;
        org.GatewayCustomerId = customer.Id;
        org.GatewaySubscriptionId = subscription.Id;

        if (subscription.Status == "incomplete" &&
            subscription.LatestInvoice?.PaymentIntent?.Status == "requires_action")
        {
            org.Enabled = false;
            return subscription.LatestInvoice.PaymentIntent.ClientSecret;
        }

        org.Enabled = true;
        org.ExpirationDate = subscription.CurrentPeriodEnd;
        return null;
    }

    #region Shared Utilities
    // We are taking only first 30 characters of the SubscriberName because stripe provide
    // for 30 characters  for custom_fields,see the link: https://stripe.com/docs/api/invoices/create
    private static string GetFirstThirtyCharacters(string subscriberName)
    {
        if (string.IsNullOrWhiteSpace(subscriberName))
        {
            return string.Empty;
        }

        return subscriberName.Length <= 30
            ? subscriberName
            : subscriberName[..30];
    }
    #endregion
}
