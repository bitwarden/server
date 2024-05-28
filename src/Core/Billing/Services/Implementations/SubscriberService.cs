using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;

using static Bit.Core.Billing.Utilities;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Services.Implementations;

public class SubscriberService(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ILogger<SubscriberService> logger,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter) : ISubscriberService
{
    public async Task CancelSubscription(
        ISubscriber subscriber,
        OffboardingSurveyResponse offboardingSurveyResponse,
        bool cancelImmediately)
    {
        var subscription = await GetSubscriptionOrThrow(subscriber);

        if (subscription.CanceledAt.HasValue ||
            subscription.Status == "canceled" ||
            subscription.Status == "unpaid" ||
            subscription.Status == "incomplete_expired")
        {
            logger.LogWarning("Cannot cancel subscription ({ID}) that's already inactive", subscription.Id);

            throw ContactSupport();
        }

        var metadata = new Dictionary<string, string>
        {
            { "cancellingUserId", offboardingSurveyResponse.UserId.ToString() }
        };

        List<string> validCancellationReasons = [
            "customer_service",
            "low_quality",
            "missing_features",
            "other",
            "switched_service",
            "too_complex",
            "too_expensive",
            "unused"
        ];

        if (cancelImmediately)
        {
            if (subscription.Metadata != null && subscription.Metadata.ContainsKey("organizationId"))
            {
                await stripeAdapter.SubscriptionUpdateAsync(subscription.Id, new SubscriptionUpdateOptions
                {
                    Metadata = metadata
                });
            }

            var options = new SubscriptionCancelOptions
            {
                CancellationDetails = new SubscriptionCancellationDetailsOptions
                {
                    Comment = offboardingSurveyResponse.Feedback
                }
            };

            if (validCancellationReasons.Contains(offboardingSurveyResponse.Reason))
            {
                options.CancellationDetails.Feedback = offboardingSurveyResponse.Reason;
            }

            await stripeAdapter.SubscriptionCancelAsync(subscription.Id, options);
        }
        else
        {
            var options = new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true,
                CancellationDetails = new SubscriptionCancellationDetailsOptions
                {
                    Comment = offboardingSurveyResponse.Feedback
                },
                Metadata = metadata
            };

            if (validCancellationReasons.Contains(offboardingSurveyResponse.Reason))
            {
                options.CancellationDetails.Feedback = offboardingSurveyResponse.Reason;
            }

            await stripeAdapter.SubscriptionUpdateAsync(subscription.Id, options);
        }
    }

    public async Task<long?> GetAccountCredit(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomer(subscriber);

        if (customer == null)
        {
            return null;
        }

        return GetFormattedAccountCredit(customer);
    }

    public async Task<Customer> GetCustomer(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            logger.LogError("Cannot retrieve customer for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            return null;
        }

        try
        {
            var customer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerGetOptions);

            if (customer != null)
            {
                return customer;
            }

            logger.LogError("Could not find Stripe customer ({CustomerID}) for subscriber ({SubscriberID})",
                subscriber.GatewayCustomerId, subscriber.Id);

            return null;
        }
        catch (StripeException exception)
        {
            logger.LogError("An error occurred while trying to retrieve Stripe customer ({CustomerID}) for subscriber ({SubscriberID}): {Error}",
                subscriber.GatewayCustomerId, subscriber.Id, exception.Message);

            return null;
        }
    }

    public async Task<Customer> GetCustomerOrThrow(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            logger.LogError("Cannot retrieve customer for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            throw ContactSupport();
        }

        try
        {
            var customer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerGetOptions);

            if (customer != null)
            {
                return customer;
            }

            logger.LogError("Could not find Stripe customer ({CustomerID}) for subscriber ({SubscriberID})",
                subscriber.GatewayCustomerId, subscriber.Id);

            throw ContactSupport();
        }
        catch (StripeException exception)
        {
            logger.LogError("An error occurred while trying to retrieve Stripe customer ({CustomerID}) for subscriber ({SubscriberID}): {Error}",
                subscriber.GatewayCustomerId, subscriber.Id, exception.Message);

            throw ContactSupport("An error occurred while trying to retrieve a Stripe Customer", exception);
        }
    }

    public async Task<PaymentInformationDTO> GetPaymentInformation(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions
        {
            Expand = ["default_source", "invoice_settings.default_payment_method", "tax_ids"]
        });

        var accountCredit = GetFormattedAccountCredit(customer);

        var paymentMethod = await GetPaymentMethodDTOAsync(subscriber.Id, customer);

        var taxInformation = GetTaxInformationDTO(customer);

        return new PaymentInformationDTO(
            accountCredit,
            paymentMethod,
            taxInformation);
    }

    public async Task<PaymentMethodDTO> GetPaymentMethod(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions
        {
            Expand = ["default_source", "invoice_settings.default_payment_method"]
        });

        return await GetPaymentMethodDTOAsync(subscriber.Id, customer);
    }

    public async Task<Subscription> GetSubscription(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            logger.LogError("Cannot retrieve subscription for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewaySubscriptionId));

            return null;
        }

        try
        {
            var subscription = await stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId, subscriptionGetOptions);

            if (subscription != null)
            {
                return subscription;
            }

            logger.LogError("Could not find Stripe subscription ({SubscriptionID}) for subscriber ({SubscriberID})",
                subscriber.GatewaySubscriptionId, subscriber.Id);

            return null;
        }
        catch (StripeException exception)
        {
            logger.LogError("An error occurred while trying to retrieve Stripe subscription ({SubscriptionID}) for subscriber ({SubscriberID}): {Error}",
                subscriber.GatewaySubscriptionId, subscriber.Id, exception.Message);

            return null;
        }
    }

    public async Task<Subscription> GetSubscriptionOrThrow(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            logger.LogError("Cannot retrieve subscription for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewaySubscriptionId));

            throw ContactSupport();
        }

        try
        {
            var subscription = await stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId, subscriptionGetOptions);

            if (subscription != null)
            {
                return subscription;
            }

            logger.LogError("Could not find Stripe subscription ({SubscriptionID}) for subscriber ({SubscriberID})",
                subscriber.GatewaySubscriptionId, subscriber.Id);

            throw ContactSupport();
        }
        catch (StripeException exception)
        {
            logger.LogError("An error occurred while trying to retrieve Stripe subscription ({SubscriptionID}) for subscriber ({SubscriberID}): {Error}",
                subscriber.GatewaySubscriptionId, subscriber.Id, exception.Message);

            throw ContactSupport("An error occurred while trying to retrieve a Stripe Subscription", exception);
        }
    }

    public async Task<TaxInformationDTO> GetTaxInformation(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions { Expand = ["tax_ids"] });

        return GetTaxInformationDTO(customer);
    }

    public async Task RemovePaymentMethod(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions
        {
            Expand = ["invoice_settings.default_payment_method", "sources"]
        });

        if (customer.Metadata?.TryGetValue(BraintreeCustomerIdKey, out var braintreeCustomerId) ?? false)
        {
            var braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

            if (braintreeCustomer == null)
            {
                logger.LogError("Failed to retrieve Braintree customer ({ID}) when removing payment method", braintreeCustomerId);

                throw ContactSupport();
            }

            if (braintreeCustomer.DefaultPaymentMethod != null)
            {
                await RemoveBraintreePaymentMethodAsync(braintreeCustomer);
            }
            else
            {
                logger.LogWarning("Tried to remove non-existent Braintree payment method for Customer ({ID})", braintreeCustomerId);
            }
        }
        else
        {
            await RemoveStripePaymentMethodsAsync(customer);
        }
    }

    public async Task UpdatePaymentMethod(
        ISubscriber subscriber,
        TokenizedPaymentMethodDTO tokenizedPaymentMethod)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(tokenizedPaymentMethod);

        var customer = await GetCustomerOrThrow(subscriber);

        var (type, token) = tokenizedPaymentMethod;

        if (string.IsNullOrEmpty(token))
        {
            logger.LogError("Updated payment method for ({SubscriberID}) must contain a token", subscriber.Id);

            throw ContactSupport();
        }

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (type)
        {
            case PaymentMethodType.BankAccount:
                {
                    var getSetupIntentsForUpdatedPaymentMethod = stripeAdapter.SetupIntentList(new SetupIntentListOptions
                    {
                        PaymentMethod = token
                    });

                    var getExistingSetupIntentsForCustomer = stripeAdapter.SetupIntentList(new SetupIntentListOptions
                    {
                        Customer = subscriber.GatewayCustomerId
                    });

                    var setupIntentsForUpdatedPaymentMethod = await getSetupIntentsForUpdatedPaymentMethod;

                    if (setupIntentsForUpdatedPaymentMethod.Count != 1)
                    {
                        logger.LogError("There were more than 1 setup intents for subscriber's ({SubscriberID}) updated payment method", subscriber.Id);

                        throw ContactSupport();
                    }

                    var matchingSetupIntent = setupIntentsForUpdatedPaymentMethod.First();

                    var existingSetupIntentsForCustomer = (await getExistingSetupIntentsForCustomer)
                        .Where(si =>
                            si.Status is "requires_payment_method" or "requires_confirmation" or "requires_action");

                    await setupIntentCache.Set(subscriber.Id, matchingSetupIntent.Id);

                    var postProcessing = existingSetupIntentsForCustomer.Select(si =>
                        stripeAdapter.SetupIntentCancel(si.Id,
                            new SetupIntentCancelOptions { CancellationReason = "abandoned" })).ToList();

                    postProcessing.Add(RemoveStripePaymentMethodsAsync(customer));

                    postProcessing.Add(RemoveBraintreeCustomerIdAsync(customer));

                    await Task.WhenAll(postProcessing);

                    break;
                }
            case PaymentMethodType.Card:
                {
                    var getExistingSetupIntentsForCustomer = stripeAdapter.SetupIntentList(new SetupIntentListOptions
                    {
                        Customer = subscriber.GatewayCustomerId
                    });

                    await RemoveStripePaymentMethodsAsync(customer);

                    await stripeAdapter.PaymentMethodAttachAsync(token,
                        new PaymentMethodAttachOptions { Customer = subscriber.GatewayCustomerId });

                    var existingSetupIntentsForCustomer = (await getExistingSetupIntentsForCustomer)
                        .Where(si =>
                            si.Status is "requires_payment_method" or "requires_confirmation" or "requires_action");

                    var postProcessing = existingSetupIntentsForCustomer.Select(si =>
                        stripeAdapter.SetupIntentCancel(si.Id,
                            new SetupIntentCancelOptions { CancellationReason = "abandoned" })).ToList();

                    var metadata = customer.Metadata;

                    if (metadata.ContainsKey(BraintreeCustomerIdKey))
                    {
                        metadata[BraintreeCustomerIdKey] = null;
                    }

                    postProcessing.Add(stripeAdapter.CustomerUpdateAsync(subscriber.GatewayCustomerId, new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = token
                        },
                        Metadata = metadata
                    }));

                    await Task.WhenAll(postProcessing);

                    break;
                }
            case PaymentMethodType.PayPal:
                {
                    var hasBraintreeCustomerId = customer.Metadata.TryGetValue(BraintreeCustomerIdKey, out var braintreeCustomerId);

                    if (hasBraintreeCustomerId)
                    {
                        var braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

                        if (braintreeCustomer == null)
                        {
                            logger.LogError("Failed to retrieve Braintree customer ({BraintreeCustomerId}) when updating payment method for subscriber ({SubscriberID})", braintreeCustomerId, subscriber.Id);

                            throw ContactSupport();
                        }

                        await ReplaceBraintreePaymentMethodAsync(braintreeCustomer, token);
                    }
                    else
                    {
                        braintreeCustomerId = await CreateBraintreeCustomerAsync(subscriber, token);
                    }

                    await AddBraintreeCustomerIdAsync(customer, braintreeCustomerId);

                    break;
                }
            default:
                {
                    logger.LogError("Cannot update subscriber's ({SubscriberID}) payment method to type ({PaymentMethodType}) as it is not supported", subscriber.Id, type.ToString());

                    throw ContactSupport();
                }
        }
    }

    public async Task UpdateTaxInformation(
        ISubscriber subscriber,
        TaxInformationDTO taxInformation)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(taxInformation);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions
        {
            Expand = ["tax_ids"]
        });

        await stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
        {
            Address = new AddressOptions
            {
                Country = taxInformation.Country,
                PostalCode = taxInformation.PostalCode,
                Line1 = taxInformation.Line1 ?? string.Empty,
                Line2 = taxInformation.Line2,
                City = taxInformation.City,
                State = taxInformation.State
            }
        });

        if (!subscriber.IsUser())
        {
            var taxId = customer.TaxIds?.FirstOrDefault();

            if (taxId != null)
            {
                await stripeAdapter.TaxIdDeleteAsync(customer.Id, taxId.Id);
            }

            var taxIdType = taxInformation.GetTaxIdType();

            if (!string.IsNullOrWhiteSpace(taxInformation.TaxId) &&
                !string.IsNullOrWhiteSpace(taxIdType))
            {
                await stripeAdapter.TaxIdCreateAsync(customer.Id, new TaxIdCreateOptions
                {
                    Type = taxIdType,
                    Value = taxInformation.TaxId,
                });
            }
        }
    }

    public async Task VerifyBankAccount(
        ISubscriber subscriber,
        (long, long) microdeposits)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var setupIntentId = await setupIntentCache.Get(subscriber.Id);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            logger.LogError("No setup intent ID exists to verify for subscriber with ID ({SubscriberID})", subscriber.Id);

            throw ContactSupport();
        }

        var (amount1, amount2) = microdeposits;

        await stripeAdapter.SetupIntentVerifyMicroDeposit(setupIntentId, new SetupIntentVerifyMicrodepositsOptions
        {
            Amounts = [amount1, amount2]
        });

        var setupIntent = await stripeAdapter.SetupIntentGet(setupIntentId);

        await stripeAdapter.PaymentMethodAttachAsync(setupIntent.PaymentMethodId, new PaymentMethodAttachOptions
        {
            Customer = subscriber.GatewayCustomerId
        });

        await stripeAdapter.CustomerUpdateAsync(subscriber.GatewayCustomerId,
            new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = setupIntent.PaymentMethodId
                }
            });
    }

    #region Utilities

    private async Task AddBraintreeCustomerIdAsync(
        Customer customer,
        string braintreeCustomerId)
    {
        var metadata = customer.Metadata;

        metadata.Add(BraintreeCustomerIdKey, braintreeCustomerId);

        await stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
        {
            Metadata = metadata
        });
    }

    private async Task<string> CreateBraintreeCustomerAsync(
        ISubscriber subscriber,
        string paymentMethodNonce)
    {
        var braintreeCustomerId =
            subscriber.BraintreeCustomerIdPrefix() +
            subscriber.Id.ToString("N").ToLower() +
            CoreHelpers.RandomString(3, upper: false, numeric: false);

        var customerResult = await braintreeGateway.Customer.CreateAsync(new CustomerRequest
        {
            Id = braintreeCustomerId,
            CustomFields = new Dictionary<string, string>
            {
                [subscriber.BraintreeIdField()] = subscriber.Id.ToString(),
                [subscriber.BraintreeCloudRegionField()] = globalSettings.BaseServiceUri.CloudRegion
            },
            Email = subscriber.BillingEmailAddress(),
            PaymentMethodNonce = paymentMethodNonce,
        });

        if (customerResult.IsSuccess())
        {
            return customerResult.Target.Id;
        }

        logger.LogError("Failed to create Braintree customer for subscriber ({ID})", subscriber.Id);

        throw ContactSupport();
    }

    private static long GetFormattedAccountCredit(
        Customer customer)
        => customer.Balance * -1 / 100;

    private async Task<PaymentMethodDTO> GetPaymentMethodDTOAsync(
        Guid subscriberId,
        Customer customer)
    {
        if (customer.Metadata != null)
        {
            var hasBraintreeCustomerId = customer.Metadata.TryGetValue(BraintreeCustomerIdKey, out var braintreeCustomerId);

            if (hasBraintreeCustomerId)
            {
                var braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

                return PaymentMethodDTO.From(braintreeCustomer);
            }
        }

        var attachedPaymentMethodDTO = PaymentMethodDTO.From(customer);

        if (attachedPaymentMethodDTO != null)
        {
            return attachedPaymentMethodDTO;
        }

        /*
         * attachedPaymentMethodDTO being null represents a case where we could be looking for the SetupIntent for an unverified "us_bank_account".
         * We store the ID of this SetupIntent in the cache when we originally update the payment method.
         */
        var setupIntentId = await setupIntentCache.Get(subscriberId);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            return null;
        }

        var setupIntent = await stripeAdapter.SetupIntentGet(setupIntentId, new SetupIntentGetOptions
        {
            Expand = ["payment_method"]
        });

        return PaymentMethodDTO.From(setupIntent);
    }

    private static TaxInformationDTO GetTaxInformationDTO(
        Customer customer)
    {
        if (customer.Address == null)
        {
            return null;
        }

        return new TaxInformationDTO(
            customer.Address.Country,
            customer.Address.PostalCode,
            customer.TaxIds?.FirstOrDefault()?.Value,
            customer.Address.Line1,
            customer.Address.Line2,
            customer.Address.City,
            customer.Address.State);
    }

    private async Task RemoveBraintreeCustomerIdAsync(
        Customer customer)
    {
        var metadata = customer.Metadata;

        if (metadata.ContainsKey(BraintreeCustomerIdKey))
        {
            metadata[BraintreeCustomerIdKey] = null;

            await stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
            {
                Metadata = metadata
            });
        }
    }

    private async Task RemoveStripePaymentMethodsAsync(
        Customer customer)
    {
        if (customer.Sources != null && customer.Sources.Any())
        {
            foreach (var source in customer.Sources)
            {
                switch (source)
                {
                    case BankAccount:
                        await stripeAdapter.BankAccountDeleteAsync(customer.Id, source.Id);
                        break;
                    case Card:
                        await stripeAdapter.CardDeleteAsync(customer.Id, source.Id);
                        break;
                }
            }
        }

        var paymentMethods = await stripeAdapter.CustomerListPaymentMethods(customer.Id);

        await Task.WhenAll(paymentMethods.Select(pm => stripeAdapter.PaymentMethodDetachAsync(pm.Id)));
    }

    private async Task RemoveBraintreePaymentMethodAsync(
        Braintree.Customer customer)
    {
        var existingDefaultPaymentMethod = customer.DefaultPaymentMethod;

        var updateCustomerResult = await braintreeGateway.Customer.UpdateAsync(
            customer.Id,
            new CustomerRequest { DefaultPaymentMethodToken = null });

        if (!updateCustomerResult.IsSuccess())
        {
            logger.LogError("Failed to remove payment method for Braintree customer ({ID}) | Error: {Error}",
                customer.Id, updateCustomerResult.Message);

            throw ContactSupport();
        }

        var deletePaymentMethodResult = await braintreeGateway.PaymentMethod.DeleteAsync(existingDefaultPaymentMethod.Token);

        if (!deletePaymentMethodResult.IsSuccess())
        {
            logger.LogWarning(
                "Failed to delete removed payment method for Braintree customer ({ID}) - outdated payment method still exists | Error: {Error}",
                customer.Id, deletePaymentMethodResult.Message);
        }
    }

    private async Task ReplaceBraintreePaymentMethodAsync(
        Braintree.Customer customer,
        string defaultPaymentMethodToken)
    {
        var existingDefaultPaymentMethod = customer.DefaultPaymentMethod;

        var createPaymentMethodResult = await braintreeGateway.PaymentMethod.CreateAsync(new PaymentMethodRequest
        {
            CustomerId = customer.Id,
            PaymentMethodNonce = defaultPaymentMethodToken
        });

        var updateCustomerResult = await braintreeGateway.Customer.UpdateAsync(
            customer.Id,
            new CustomerRequest { DefaultPaymentMethodToken = createPaymentMethodResult.Target.Token });

        if (!updateCustomerResult.IsSuccess())
        {
            logger.LogError("Failed to replace payment method for Braintree customer ({ID}) - deleting created payment method | Error: {Error}",
                customer.Id, updateCustomerResult.Message);

            await braintreeGateway.PaymentMethod.DeleteAsync(createPaymentMethodResult.Target.Token);

            throw ContactSupport();
        }

        if (existingDefaultPaymentMethod != null)
        {
            var deletePaymentMethodResult = await braintreeGateway.PaymentMethod.DeleteAsync(existingDefaultPaymentMethod.Token);

            if (!deletePaymentMethodResult.IsSuccess())
            {
                logger.LogWarning(
                    "Failed to delete replaced payment method for Braintree customer ({ID}) - outdated payment method still exists | Error: {Error}",
                    customer.Id, deletePaymentMethodResult.Message);
            }
        }
    }

    #endregion
}
