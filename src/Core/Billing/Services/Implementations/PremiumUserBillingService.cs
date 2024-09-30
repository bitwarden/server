using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Services.Implementations;

using static Utilities;

public class PremiumUserBillingService(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ILogger<PremiumUserBillingService> logger,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserRepository userRepository) : IPremiumUserBillingService
{
    public async Task Finalize(PremiumUserSale sale)
    {
        var (user, customerSetup, storage) = sale;

        List<string> expand = ["tax"];

        var customer = string.IsNullOrEmpty(user.GatewayCustomerId)
            ? await CreateCustomerAsync(user, customerSetup)
            : await subscriberService.GetCustomerOrThrow(user, new CustomerGetOptions { Expand = expand });

        var subscription = await CreateSubscriptionAsync(user.Id, customer, storage);

        switch (customerSetup.TokenizedPaymentSource)
        {
            case { Type: PaymentMethodType.PayPal }
                when subscription.Status == StripeConstants.SubscriptionStatus.Incomplete:
            case { Type: not PaymentMethodType.PayPal }
                when subscription.Status == StripeConstants.SubscriptionStatus.Active:
                {
                    user.Premium = true;
                    user.PremiumExpirationDate = subscription.CurrentPeriodEnd;
                    break;
                }
        }

        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = customer.Id;
        user.GatewaySubscriptionId = subscription.Id;

        await userRepository.ReplaceAsync(user);
    }

    private async Task<Customer> CreateCustomerAsync(
        User user,
        CustomerSetup customerSetup)
    {
        if (customerSetup.TokenizedPaymentSource is not
            {
                Type: PaymentMethodType.BankAccount or PaymentMethodType.Card or PaymentMethodType.PayPal,
                Token: not null and not ""
            })
        {
            logger.LogError(
                "Cannot create customer for user ({UserID}) without a valid payment source", user.Id);

            throw new BillingException();
        }

        if (customerSetup.TaxInformation is not { Country: not null and not "", PostalCode: not null and not "" })
        {
            logger.LogError(
                "Cannot create customer for user ({UserID}) without valid tax information", user.Id);

            throw new BillingException();
        }

        var (address, taxIdData) = customerSetup.TaxInformation.GetStripeOptions();

        var subscriberName = user.SubscriberName();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = address,
            Description = user.Name,
            Email = user.Email,
            Expand = ["tax"],
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
                ["region"] = globalSettings.BaseServiceUri.CloudRegion,
                ["userId"] = user.Id.ToString()
            },
            Tax = new CustomerTaxOptions
            {
                ValidateLocation = StripeConstants.ValidateTaxLocationTiming.Immediately
            },
            TaxIdData = taxIdData
        };

        var (paymentMethodType, paymentMethodToken) = customerSetup.TokenizedPaymentSource;

        var braintreeCustomerId = "";

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (paymentMethodType)
        {
            case PaymentMethodType.BankAccount:
                {
                    var setupIntent =
                        (await stripeAdapter.SetupIntentList(new SetupIntentListOptions { PaymentMethod = paymentMethodToken }))
                        .FirstOrDefault();

                    if (setupIntent == null)
                    {
                        logger.LogError("Cannot create customer for user ({UserID}) without a setup intent for their bank account", user.Id);
                        throw new BillingException();
                    }

                    await setupIntentCache.Set(user.Id, setupIntent.Id);
                    break;
                }
            case PaymentMethodType.Card:
                {
                    customerCreateOptions.PaymentMethod = paymentMethodToken;
                    customerCreateOptions.InvoiceSettings.DefaultPaymentMethod = paymentMethodToken;
                    break;
                }
            case PaymentMethodType.PayPal:
                {
                    braintreeCustomerId = await subscriberService.CreateBraintreeCustomer(user, paymentMethodToken);
                    customerCreateOptions.Metadata[BraintreeCustomerIdKey] = braintreeCustomerId;
                    break;
                }
            default:
                {
                    logger.LogError("Cannot create customer for user ({UserID}) using payment method type ({PaymentMethodType}) as it is not supported", user.Id, paymentMethodType.ToString());
                    throw new BillingException();
                }
        }

        try
        {
            return await stripeAdapter.CustomerCreateAsync(customerCreateOptions);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code ==
                                                      StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            await Revert();
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid.");
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code ==
                                                      StripeConstants.ErrorCodes.TaxIdInvalid)
        {
            await Revert();
            throw new BadRequestException(
                "Your tax ID wasn't recognized for your selected country. Please ensure your country and tax ID are valid.");
        }
        catch
        {
            await Revert();
            throw;
        }

        async Task Revert()
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (customerSetup.TokenizedPaymentSource!.Type)
            {
                case PaymentMethodType.BankAccount:
                    {
                        await setupIntentCache.Remove(user.Id);
                        break;
                    }
                case PaymentMethodType.PayPal when !string.IsNullOrEmpty(braintreeCustomerId):
                    {
                        await braintreeGateway.Customer.DeleteAsync(braintreeCustomerId);
                        break;
                    }
            }
        }
    }

    private async Task<Subscription> CreateSubscriptionAsync(
        Guid userId,
        Customer customer,
        int? storage)
    {
        var subscriptionItemOptionsList = new List<SubscriptionItemOptions>
        {
            new ()
            {
                Price = "premium-annually",
                Quantity = 1
            }
        };

        if (storage is > 0)
        {
            subscriptionItemOptionsList.Add(new SubscriptionItemOptions
            {
                Price = "storage-gb-annually",
                Quantity = storage
            });
        }

        var usingPayPal = customer.Metadata?.ContainsKey(BraintreeCustomerIdKey) ?? false;

        var subscriptionCreateOptions = new SubscriptionCreateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = customer.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported,
            },
            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
            Customer = customer.Id,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.ToString()
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
