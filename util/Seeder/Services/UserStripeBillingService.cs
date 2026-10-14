using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Seeder.Data.Enums;
using Stripe;

namespace Bit.Seeder.Services;

/// <summary>
/// Creates a Stripe customer and an active subscription for a seeded individual user in the shared Stripe
/// <c>sk_test_</c> account.
/// </summary>
/// <remarks>
/// Doesn't reuse <c>CreatePremiumCloudHostedSubscriptionCommand</c> because that command injects
/// <c>IUserService</c>, which the Seeder never references. The requests copy that command's shape instead.
/// </remarks>
public sealed class UserStripeBillingService(
    GlobalSettings globalSettings,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : IUserStripeBillingService
{
    /// <summary>
    /// Throws when the host is not configured for Stripe test-mode billing.
    /// </summary>
    public void ValidateConfiguration() => StripeSettingValidator.Validate(globalSettings);

    /// <summary>
    /// Creates the user's Stripe customer and subscription, then sets their subscription and gateway fields.
    /// </summary>
    public async Task CreateSubscriptionAsync(User user, UserSubscriptionType subscriptionType)
    {
        ValidateConfiguration();

        Customer? customer = null;
        try
        {
            var plan = subscriptionType switch
            {
                UserSubscriptionType.Premium => await pricingClient.GetAvailablePremiumPlan(),
                _ => throw new ArgumentOutOfRangeException(nameof(subscriptionType), subscriptionType, null),
            };

            customer = await stripeAdapter.CreateCustomerAsync(new CustomerCreateOptions
            {
                Address = new AddressOptions { Country = "US", PostalCode = "43432" },
                Description = user.Name,
                Email = user.Email,
                InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = "pm_card_visa" },
                Metadata = new Dictionary<string, string>
                {
                    [StripeConstants.MetadataKeys.Region] = globalSettings.BaseServiceUri.CloudRegion,
                    [StripeConstants.MetadataKeys.UserId] = user.Id.ToString(),
                },
                PaymentMethod = "pm_card_visa",
                Tax = new CustomerTaxOptions
                {
                    ValidateLocation = StripeConstants.ValidateTaxLocationTiming.Immediately,
                },
            });

            var subscription = await stripeAdapter.CreateSubscriptionAsync(new SubscriptionCreateOptions
            {
                AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
                BillingMode = new SubscriptionBillingModeOptions { Type = StripeConstants.BillingMode.Classic },
                CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
                Customer = customer.Id,
                Items = [new SubscriptionItemOptions { Price = plan.Seat.StripePriceId, Quantity = 1 }],
                Metadata = new Dictionary<string, string>
                {
                    [StripeConstants.MetadataKeys.UserId] = user.Id.ToString(),
                },
                OffSession = true,
            });

            user.Premium = true;
            user.PremiumExpirationDate = subscription.GetCurrentPeriodEnd();
            user.Gateway = GatewayType.Stripe;
            user.GatewayCustomerId = customer.Id;
            user.GatewaySubscriptionId = subscription.Id;
            user.MaxStorageGb = (short)plan.Storage.Provided;
            user.LicenseKey = CoreHelpers.SecureRandomString(20);
        }
        catch (Exception ex) when (ex is StripeException or BillingException or NotFoundException
            or HttpRequestException)
        {
            throw new InvalidOperationException(
                $"Stripe billing failed for premium user '{user.Id}'. Nothing was committed to the database. " +
                $"Gateway customer: {customer?.Id ?? "<none>"}. Cancel any orphaned Stripe customer before " +
                $"re-seeding: {ex.Message}", ex);
        }
    }
}
