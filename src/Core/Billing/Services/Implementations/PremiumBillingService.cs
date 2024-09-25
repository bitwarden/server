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

using static Bit.Core.Billing.Utilities;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Services.Implementations;

#nullable enable

public class PremiumBillingService(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ILogger<PremiumBillingService> logger,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserRepository userRepository) : IPremiumBillingService
{
    public async Task Finalize(PremiumSale sale)
    {
        var (user, paymentSetup, storage) = sale;

        List<string> expand = ["tax"];

        var customer = string.IsNullOrEmpty(user.GatewayCustomerId)
            ? await CreateCustomerAsync(user, paymentSetup, expand)
            : await subscriberService.GetCustomerOrThrow(user, new CustomerGetOptions { Expand = expand });

        var subscription = await CreateSubscriptionAsync(user.Id, customer, storage);

        switch (paymentSetup.TokenizedPaymentSource)
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

    #region Utilities

    private async Task<Customer> CreateCustomerAsync(
        User user,
        PaymentSetup paymentSetup,
        List<string>? expand = null)
    {
        if (paymentSetup.TokenizedPaymentSource is not
            {
                Type: PaymentMethodType.BankAccount or PaymentMethodType.Card or PaymentMethodType.PayPal,
                Token: not null and not ""
            })
        {
            logger.LogError(
                "Cannot create customer for user ({UserID}) without a valid payment source", user.Id);

            throw new BillingException();
        }

        if (paymentSetup.TaxInformation is not { Country: not null and not "", PostalCode: not null and not "" })
        {
            logger.LogError(
                "Cannot create customer for user ({UserID}) without valid tax information", user.Id);

            throw new BillingException();
        }

        var (address, taxIdData) = paymentSetup.TaxInformation.GetStripeOptions();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = address,
            Description = user.Name,
            Email = user.Email,
            Expand = expand,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = user.SubscriberType(),
                        Value = user.SubscriberName().Length <= 30
                            ? user.SubscriberName()
                            : user.SubscriberName()[..30]
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

        var (type, token) = paymentSetup.TokenizedPaymentSource;

        var braintreeCustomerId = "";

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (type)
        {
            case PaymentMethodType.BankAccount:
            {
                var setupIntent =
                    (await stripeAdapter.SetupIntentList(new SetupIntentListOptions { PaymentMethod = token }))
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
                customerCreateOptions.PaymentMethod = token;
                customerCreateOptions.InvoiceSettings.DefaultPaymentMethod = token;
                break;
            }
            case PaymentMethodType.PayPal:
            {
                braintreeCustomerId = await subscriberService.CreateBraintreeCustomer(user, token);
                customerCreateOptions.Metadata[BraintreeCustomerIdKey] = braintreeCustomerId;
                break;
            }
            default:
            {
                logger.LogError("Cannot create customer for user ({UserID}) using payment method type ({PaymentMethodType}) as it is not supported", user.Id, type.ToString());
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
            switch (paymentSetup.TokenizedPaymentSource.Type)
            {
                case PaymentMethodType.BankAccount:
                {
                    await setupIntentCache.Remove(user.Id);
                    break;
                }
                case PaymentMethodType.PayPal:
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

    #endregion
}
