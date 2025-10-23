using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Braintree;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Premium.Commands;

using static Utilities;

/// <summary>
/// Creates a premium subscription for a cloud-hosted user with Stripe payment processing.
/// Handles customer creation, payment method setup, and subscription creation.
/// </summary>
public interface ICreatePremiumCloudHostedSubscriptionCommand
{
    /// <summary>
    /// Creates a premium cloud-hosted subscription for the specified user.
    /// </summary>
    /// <param name="user">The user to create the premium subscription for. Must not already be a premium user.</param>
    /// <param name="paymentMethod">The tokenized payment method containing the payment type and token for billing.</param>
    /// <param name="billingAddress">The billing address information required for tax calculation and customer creation.</param>
    /// <param name="additionalStorageGb">Additional storage in GB beyond the base 1GB included with premium (must be >= 0).</param>
    /// <returns>A billing command result indicating success or failure with appropriate error details.</returns>
    Task<BillingCommandResult<None>> Run(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress,
        short additionalStorageGb);
}

public class CreatePremiumCloudHostedSubscriptionCommand(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserService userService,
    IPushNotificationService pushNotificationService,
    ILogger<CreatePremiumCloudHostedSubscriptionCommand> logger,
    IPricingClient pricingClient)
    : BaseBillingCommand<CreatePremiumCloudHostedSubscriptionCommand>(logger), ICreatePremiumCloudHostedSubscriptionCommand
{
    private static readonly List<string> _expand = ["tax"];
    private readonly ILogger<CreatePremiumCloudHostedSubscriptionCommand> _logger = logger;

    public Task<BillingCommandResult<None>> Run(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress,
        short additionalStorageGb) => HandleAsync<None>(async () =>
    {
        if (user.Premium)
        {
            return new BadRequest("Already a premium user.");
        }

        if (additionalStorageGb < 0)
        {
            return new BadRequest("Additional storage must be greater than 0.");
        }

        var customer = string.IsNullOrEmpty(user.GatewayCustomerId)
            ? await CreateCustomerAsync(user, paymentMethod, billingAddress)
            : await subscriberService.GetCustomerOrThrow(user, new CustomerGetOptions { Expand = _expand });

        customer = await ReconcileBillingLocationAsync(customer, billingAddress);

        var subscription = await CreateSubscriptionAsync(user.Id, customer, additionalStorageGb > 0 ? additionalStorageGb : null);

        switch (paymentMethod)
        {
            case { Type: TokenizablePaymentMethodType.PayPal }
                when subscription.Status == StripeConstants.SubscriptionStatus.Incomplete:
            case { Type: not TokenizablePaymentMethodType.PayPal }
                when subscription.Status == StripeConstants.SubscriptionStatus.Active:
                {
                    user.Premium = true;
                    user.PremiumExpirationDate = subscription.GetCurrentPeriodEnd();
                    break;
                }
        }

        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = customer.Id;
        user.GatewaySubscriptionId = subscription.Id;
        user.MaxStorageGb = (short)(1 + additionalStorageGb);
        user.LicenseKey = CoreHelpers.SecureRandomString(20);
        user.RevisionDate = DateTime.UtcNow;

        await userService.SaveUserAsync(user);
        await pushNotificationService.PushSyncVaultAsync(user.Id);

        return new None();
    });

    private async Task<Customer> CreateCustomerAsync(User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        var subscriberName = user.SubscriberName();
        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = new AddressOptions
            {
                Line1 = billingAddress.Line1,
                Line2 = billingAddress.Line2,
                City = billingAddress.City,
                PostalCode = billingAddress.PostalCode,
                State = billingAddress.State,
                Country = billingAddress.Country
            },
            Description = user.Name,
            Email = user.Email,
            Expand = _expand,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = user.SubscriberType(),
                        Value = subscriberName.Length <= 30
                            ? subscriberName
                            : subscriberName[..30]
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.Region] = globalSettings.BaseServiceUri.CloudRegion,
                [StripeConstants.MetadataKeys.UserId] = user.Id.ToString()
            },
            Tax = new CustomerTaxOptions
            {
                ValidateLocation = StripeConstants.ValidateTaxLocationTiming.Immediately
            }
        };

        var braintreeCustomerId = "";

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (paymentMethod.Type)
        {
            case TokenizablePaymentMethodType.BankAccount:
                {
                    var setupIntent =
                        (await stripeAdapter.SetupIntentList(new SetupIntentListOptions { PaymentMethod = paymentMethod.Token }))
                        .FirstOrDefault();

                    if (setupIntent == null)
                    {
                        _logger.LogError("Cannot create customer for user ({UserID}) without a setup intent for their bank account", user.Id);
                        throw new BillingException();
                    }

                    await setupIntentCache.Set(user.Id, setupIntent.Id);
                    break;
                }
            case TokenizablePaymentMethodType.Card:
                {
                    customerCreateOptions.PaymentMethod = paymentMethod.Token;
                    customerCreateOptions.InvoiceSettings.DefaultPaymentMethod = paymentMethod.Token;
                    break;
                }
            case TokenizablePaymentMethodType.PayPal:
                {
                    braintreeCustomerId = await subscriberService.CreateBraintreeCustomer(user, paymentMethod.Token);
                    customerCreateOptions.Metadata[BraintreeCustomerIdKey] = braintreeCustomerId;
                    break;
                }
            default:
                {
                    _logger.LogError("Cannot create customer for user ({UserID}) using payment method type ({PaymentMethodType}) as it is not supported", user.Id, paymentMethod.Type.ToString());
                    throw new BillingException();
                }
        }

        try
        {
            return await stripeAdapter.CustomerCreateAsync(customerCreateOptions);
        }
        catch
        {
            await Revert();
            throw;
        }

        async Task Revert()
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (paymentMethod.Type)
            {
                case TokenizablePaymentMethodType.BankAccount:
                    {
                        await setupIntentCache.RemoveSetupIntentForSubscriber(user.Id);
                        break;
                    }
                case TokenizablePaymentMethodType.PayPal when !string.IsNullOrEmpty(braintreeCustomerId):
                    {
                        await braintreeGateway.Customer.DeleteAsync(braintreeCustomerId);
                        break;
                    }
            }
        }
    }

    private async Task<Customer> ReconcileBillingLocationAsync(
        Customer customer,
        BillingAddress billingAddress)
    {
        /*
         * If the customer was previously set up with credit, which does not require a billing location,
         * we need to update the customer on the fly before we start the subscription.
         */
        if (customer is { Address: { Country: not null and not "", PostalCode: not null and not "" } })
        {
            return customer;
        }

        var options = new CustomerUpdateOptions
        {
            Address = new AddressOptions
            {
                Line1 = billingAddress.Line1,
                Line2 = billingAddress.Line2,
                City = billingAddress.City,
                PostalCode = billingAddress.PostalCode,
                State = billingAddress.State,
                Country = billingAddress.Country
            },
            Expand = _expand,
            Tax = new CustomerTaxOptions
            {
                ValidateLocation = StripeConstants.ValidateTaxLocationTiming.Immediately
            }
        };
        return await stripeAdapter.CustomerUpdateAsync(customer.Id, options);
    }

    private async Task<Subscription> CreateSubscriptionAsync(
        Guid userId,
        Customer customer,
        int? storage)
    {
        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();

        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>
        {
            new ()
            {
                Price = premiumPlan.Seat.StripePriceId,
                Quantity = 1
            }
        };

        if (storage is > 0)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = premiumPlan.Storage.StripePriceId,
                Quantity = storage
            });
        }

        var usingPayPal = customer.Metadata?.ContainsKey(BraintreeCustomerIdKey) ?? false;

        var subscriptionCreateOptions = new SubscriptionCreateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = true
            },
            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
            Customer = customer.Id,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.UserId] = userId.ToString()
            },
            PaymentBehavior = usingPayPal
                ? StripeConstants.PaymentBehavior.DefaultIncomplete
                : null,
            OffSession = true
        };

        var subscription = await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);

        if (usingPayPal)
        {
            await stripeAdapter.InvoiceUpdateAsync(subscription.LatestInvoiceId, new InvoiceUpdateOptions
            {
                AutoAdvance = false
            });
        }

        return subscription;
    }
}
