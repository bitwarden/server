using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;

using static Bit.Core.Billing.Utilities;
using Customer = Stripe.Customer;
using PaymentMethod = Bit.Core.Billing.Models.PaymentMethod;
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

            throw new BillingException();
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

    public async Task<string> CreateBraintreeCustomer(
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

        throw new BillingException();
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

            throw new BillingException();
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

            throw new BillingException();
        }
        catch (StripeException stripeException)
        {
            logger.LogError("An error occurred while trying to retrieve Stripe customer ({CustomerID}) for subscriber ({SubscriberID}): {Error}",
                subscriber.GatewayCustomerId, subscriber.Id, stripeException.Message);

            throw new BillingException(
                message: "An error occurred while trying to retrieve a Stripe customer",
                innerException: stripeException);
        }
    }

    public async Task<PaymentMethod> GetPaymentMethod(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomer(subscriber, new CustomerGetOptions
        {
            Expand = ["default_source", "invoice_settings.default_payment_method", "subscriptions", "tax_ids"]
        });

        if (customer == null)
        {
            return PaymentMethod.Empty;
        }

        var accountCredit = customer.Balance * -1 / 100;

        var paymentMethod = await GetPaymentSourceAsync(subscriber.Id, customer);

        var subscriptionStatus = customer.Subscriptions
            .FirstOrDefault(subscription => subscription.Id == subscriber.GatewaySubscriptionId)?
            .Status;

        var taxInformation = GetTaxInformation(customer);

        return new PaymentMethod(
            accountCredit,
            paymentMethod,
            subscriptionStatus,
            taxInformation);
    }

    public async Task<PaymentSource> GetPaymentSource(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions
        {
            Expand = ["default_source", "invoice_settings.default_payment_method"]
        });

        return await GetPaymentSourceAsync(subscriber.Id, customer);
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

            throw new BillingException();
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

            throw new BillingException();
        }
        catch (StripeException stripeException)
        {
            logger.LogError("An error occurred while trying to retrieve Stripe subscription ({SubscriptionID}) for subscriber ({SubscriberID}): {Error}",
                subscriber.GatewaySubscriptionId, subscriber.Id, stripeException.Message);

            throw new BillingException(
                message: "An error occurred while trying to retrieve a Stripe subscription",
                innerException: stripeException);
        }
    }

    public async Task<TaxInformation> GetTaxInformation(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions { Expand = ["tax_ids"] });

        return GetTaxInformation(customer);
    }

    public async Task RemovePaymentSource(
        ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            throw new BillingException();
        }

        var stripeCustomer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions
        {
            Expand = ["invoice_settings.default_payment_method", "sources"]
        });

        if (stripeCustomer.Metadata?.TryGetValue(BraintreeCustomerIdKey, out var braintreeCustomerId) ?? false)
        {
            var braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

            if (braintreeCustomer == null)
            {
                logger.LogError("Failed to retrieve Braintree customer ({ID}) when removing payment method", braintreeCustomerId);

                throw new BillingException();
            }

            if (braintreeCustomer.DefaultPaymentMethod != null)
            {
                var existingDefaultPaymentMethod = braintreeCustomer.DefaultPaymentMethod;

                var updateCustomerResult = await braintreeGateway.Customer.UpdateAsync(
                    braintreeCustomerId,
                    new CustomerRequest { DefaultPaymentMethodToken = null });

                if (!updateCustomerResult.IsSuccess())
                {
                    logger.LogError("Failed to update payment method for Braintree customer ({ID}) | Message: {Message}",
                        braintreeCustomerId, updateCustomerResult.Message);

                    throw new BillingException();
                }

                var deletePaymentMethodResult = await braintreeGateway.PaymentMethod.DeleteAsync(existingDefaultPaymentMethod.Token);

                if (!deletePaymentMethodResult.IsSuccess())
                {
                    await braintreeGateway.Customer.UpdateAsync(
                        braintreeCustomerId,
                        new CustomerRequest { DefaultPaymentMethodToken = existingDefaultPaymentMethod.Token });

                    logger.LogError(
                        "Failed to delete Braintree payment method for Customer ({ID}), re-linked payment method. Message: {Message}",
                        braintreeCustomerId, deletePaymentMethodResult.Message);

                    throw new BillingException();
                }
            }
            else
            {
                logger.LogWarning("Tried to remove non-existent Braintree payment method for Customer ({ID})", braintreeCustomerId);
            }
        }
        else
        {
            if (stripeCustomer.Sources != null && stripeCustomer.Sources.Any())
            {
                foreach (var source in stripeCustomer.Sources)
                {
                    switch (source)
                    {
                        case BankAccount:
                            await stripeAdapter.BankAccountDeleteAsync(stripeCustomer.Id, source.Id);
                            break;
                        case Card:
                            await stripeAdapter.CardDeleteAsync(stripeCustomer.Id, source.Id);
                            break;
                    }
                }
            }

            var paymentMethods = stripeAdapter.PaymentMethodListAutoPagingAsync(new PaymentMethodListOptions
            {
                Customer = stripeCustomer.Id
            });

            await foreach (var paymentMethod in paymentMethods)
            {
                await stripeAdapter.PaymentMethodDetachAsync(paymentMethod.Id);
            }
        }
    }

    public async Task UpdatePaymentSource(
        ISubscriber subscriber,
        TokenizedPaymentSource tokenizedPaymentSource)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(tokenizedPaymentSource);

        var customer = await GetCustomerOrThrow(subscriber);

        var (type, token) = tokenizedPaymentSource;

        if (string.IsNullOrEmpty(token))
        {
            logger.LogError("Updated payment method for ({SubscriberID}) must contain a token", subscriber.Id);

            throw new BillingException();
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

                    // Find the setup intent for the incoming payment method token.
                    var setupIntentsForUpdatedPaymentMethod = await getSetupIntentsForUpdatedPaymentMethod;

                    if (setupIntentsForUpdatedPaymentMethod.Count != 1)
                    {
                        logger.LogError("There were more than 1 setup intents for subscriber's ({SubscriberID}) updated payment method", subscriber.Id);

                        throw new BillingException();
                    }

                    var matchingSetupIntent = setupIntentsForUpdatedPaymentMethod.First();

                    // Find the customer's existing setup intents that should be cancelled.
                    var existingSetupIntentsForCustomer = (await getExistingSetupIntentsForCustomer)
                        .Where(si =>
                            si.Status is "requires_payment_method" or "requires_confirmation" or "requires_action");

                    // Store the incoming payment method's setup intent ID in the cache for the subscriber so it can be verified later.
                    await setupIntentCache.Set(subscriber.Id, matchingSetupIntent.Id);

                    // Cancel the customer's other open setup intents.
                    var postProcessing = existingSetupIntentsForCustomer.Select(si =>
                        stripeAdapter.SetupIntentCancel(si.Id,
                            new SetupIntentCancelOptions { CancellationReason = "abandoned" })).ToList();

                    // Remove the customer's other attached Stripe payment methods.
                    postProcessing.Add(RemoveStripePaymentMethodsAsync(customer));

                    // Remove the customer's Braintree customer ID.
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

                    // Remove the customer's other attached Stripe payment methods.
                    await RemoveStripePaymentMethodsAsync(customer);

                    // Attach the incoming payment method.
                    await stripeAdapter.PaymentMethodAttachAsync(token,
                        new PaymentMethodAttachOptions { Customer = subscriber.GatewayCustomerId });

                    // Find the customer's existing setup intents that should be cancelled.
                    var existingSetupIntentsForCustomer = (await getExistingSetupIntentsForCustomer)
                        .Where(si =>
                            si.Status is "requires_payment_method" or "requires_confirmation" or "requires_action");

                    // Cancel the customer's other open setup intents.
                    var postProcessing = existingSetupIntentsForCustomer.Select(si =>
                        stripeAdapter.SetupIntentCancel(si.Id,
                            new SetupIntentCancelOptions { CancellationReason = "abandoned" })).ToList();

                    var metadata = customer.Metadata;

                    if (metadata.TryGetValue(BraintreeCustomerIdKey, out var value))
                    {
                        metadata[BraintreeCustomerIdOldKey] = value;
                        metadata[BraintreeCustomerIdKey] = null;
                    }

                    // Set the customer's default payment method in Stripe and remove their Braintree customer ID.
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
                    string braintreeCustomerId;

                    if (customer.Metadata != null)
                    {
                        var hasBraintreeCustomerId = customer.Metadata.TryGetValue(BraintreeCustomerIdKey, out braintreeCustomerId);

                        if (hasBraintreeCustomerId)
                        {
                            var braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

                            if (braintreeCustomer == null)
                            {
                                logger.LogError("Failed to retrieve Braintree customer ({BraintreeCustomerId}) when updating payment method for subscriber ({SubscriberID})", braintreeCustomerId, subscriber.Id);

                                throw new BillingException();
                            }

                            await ReplaceBraintreePaymentMethodAsync(braintreeCustomer, token);

                            return;
                        }
                    }

                    braintreeCustomerId = await CreateBraintreeCustomer(subscriber, token);

                    await AddBraintreeCustomerIdAsync(customer, braintreeCustomerId);

                    break;
                }
            default:
                {
                    logger.LogError("Cannot update subscriber's ({SubscriberID}) payment method to type ({PaymentMethodType}) as it is not supported", subscriber.Id, type.ToString());

                    throw new BillingException();
                }
        }
    }

    public async Task UpdateTaxInformation(
        ISubscriber subscriber,
        TaxInformation taxInformation)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(taxInformation);

        var customer = await GetCustomerOrThrow(subscriber, new CustomerGetOptions
        {
            Expand = ["subscriptions", "tax", "tax_ids"]
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

        if (SubscriberIsEligibleForAutomaticTax(subscriber, customer))
        {
            await stripeAdapter.SubscriptionUpdateAsync(subscriber.GatewaySubscriptionId,
                new SubscriptionUpdateOptions
                {
                    AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
                    DefaultTaxRates = []
                });
        }

        return;

        bool SubscriberIsEligibleForAutomaticTax(ISubscriber localSubscriber, Customer localCustomer)
            => !string.IsNullOrEmpty(localSubscriber.GatewaySubscriptionId) &&
               (localCustomer.Subscriptions?.Any(sub => sub.Id == localSubscriber.GatewaySubscriptionId && !sub.AutomaticTax.Enabled) ?? false) &&
               localCustomer.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported;
    }

    public async Task VerifyBankAccount(
        ISubscriber subscriber,
        string descriptorCode)
    {
        var setupIntentId = await setupIntentCache.Get(subscriber.Id);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            logger.LogError("No setup intent ID exists to verify for subscriber with ID ({SubscriberID})", subscriber.Id);
            throw new BillingException();
        }

        try
        {
            await stripeAdapter.SetupIntentVerifyMicroDeposit(setupIntentId,
                new SetupIntentVerifyMicrodepositsOptions { DescriptorCode = descriptorCode });

            var setupIntent = await stripeAdapter.SetupIntentGet(setupIntentId);

            await stripeAdapter.PaymentMethodAttachAsync(setupIntent.PaymentMethodId,
                new PaymentMethodAttachOptions { Customer = subscriber.GatewayCustomerId });

            await stripeAdapter.CustomerUpdateAsync(subscriber.GatewayCustomerId,
                new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = setupIntent.PaymentMethodId
                    }
                });
        }
        catch (StripeException stripeException)
        {
            if (!string.IsNullOrEmpty(stripeException.StripeError?.Code))
            {
                var message = stripeException.StripeError.Code switch
                {
                    StripeConstants.ErrorCodes.PaymentMethodMicroDepositVerificationAttemptsExceeded => "You have exceeded the number of allowed verification attempts. Please contact support.",
                    StripeConstants.ErrorCodes.PaymentMethodMicroDepositVerificationDescriptorCodeMismatch => "The verification code you provided does not match the one sent to your bank account. Please try again.",
                    StripeConstants.ErrorCodes.PaymentMethodMicroDepositVerificationTimeout => "Your bank account was not verified within the required time period. Please contact support.",
                    _ => BillingException.DefaultMessage
                };

                throw new BadRequestException(message);
            }

            logger.LogError(stripeException, "An unhandled Stripe exception was thrown while verifying subscriber's ({SubscriberID}) bank account", subscriber.Id);
            throw new BillingException();
        }
    }

    #region Shared Utilities

    private async Task AddBraintreeCustomerIdAsync(
        Customer customer,
        string braintreeCustomerId)
    {
        var metadata = customer.Metadata ?? new Dictionary<string, string>();

        metadata[BraintreeCustomerIdKey] = braintreeCustomerId;

        await stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions
        {
            Metadata = metadata
        });
    }

    private async Task<PaymentSource> GetPaymentSourceAsync(
        Guid subscriberId,
        Customer customer)
    {
        if (customer.Metadata != null)
        {
            var hasBraintreeCustomerId = customer.Metadata.TryGetValue(BraintreeCustomerIdKey, out var braintreeCustomerId);

            if (hasBraintreeCustomerId)
            {
                var braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

                return PaymentSource.From(braintreeCustomer);
            }
        }

        var attachedPaymentMethodDTO = PaymentSource.From(customer);

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

        return PaymentSource.From(setupIntent);
    }

    private static TaxInformation GetTaxInformation(
        Customer customer)
    {
        if (customer.Address == null)
        {
            return null;
        }

        return new TaxInformation(
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
        var metadata = customer.Metadata ?? new Dictionary<string, string>();

        if (metadata.TryGetValue(BraintreeCustomerIdKey, out var value))
        {
            metadata[BraintreeCustomerIdOldKey] = value;
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

        if (!createPaymentMethodResult.IsSuccess())
        {
            logger.LogError("Failed to replace payment method for Braintree customer ({ID}) - Creation of new payment method failed | Error: {Error}", customer.Id, createPaymentMethodResult.Message);

            throw new BillingException();
        }

        var updateCustomerResult = await braintreeGateway.Customer.UpdateAsync(
            customer.Id,
            new CustomerRequest { DefaultPaymentMethodToken = createPaymentMethodResult.Target.Token });

        if (!updateCustomerResult.IsSuccess())
        {
            logger.LogError("Failed to replace payment method for Braintree customer ({ID}) - Customer update failed | Error: {Error}",
                customer.Id, updateCustomerResult.Message);

            await braintreeGateway.PaymentMethod.DeleteAsync(createPaymentMethodResult.Target.Token);

            throw new BillingException();
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
