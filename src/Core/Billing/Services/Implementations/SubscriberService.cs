﻿using Bit.Core.Billing.Caches;
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
using PaymentMethod = Bit.Core.Billing.Models.PaymentMethod;

namespace Bit.Core.Billing.Services.Implementations;

#nullable enable

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
        if (subscriber is not { GatewaySubscriptionId: not null })
        {
            logger.LogError("Cannot cancel subscription for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewaySubscriptionId));

            throw new BillingException();
        }

        var subscription = await stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId);

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

    public async Task<PaymentMethod> GetPaymentMethod(
        ISubscriber subscriber)
    {
        if (subscriber is not { GatewayCustomerId: not null })
        {
            logger.LogError("Cannot retrieve payment information for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            throw new BillingException();
        }

        var customer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, new CustomerGetOptions
        {
            Expand = ["default_source", "invoice_settings.default_payment_method", "tax_ids"]
        });

        var accountCredit = customer.Balance * -1 / 100;

        var paymentMethod = await GetPaymentSourceAsync(subscriber.Id, customer);

        var taxInformation = GetTaxInformation(customer);

        return new PaymentMethod(
            accountCredit,
            paymentMethod,
            taxInformation);
    }

    public async Task RemovePaymentSource(
        ISubscriber subscriber)
    {
        if (subscriber is not { GatewayCustomerId: not null })
        {
            logger.LogError("Cannot remove payment method for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            throw new BillingException();
        }

        var stripeCustomer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, new CustomerGetOptions
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
        if (subscriber is not { GatewayCustomerId: not null })
        {
            logger.LogError("Cannot update payment method for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            throw new BillingException();
        }

        var customer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId);

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

                    if (metadata.ContainsKey(BraintreeCustomerIdKey))
                    {
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
                    if (customer.Metadata != null)
                    {
                        var hasBraintreeCustomerId = customer.Metadata.TryGetValue(BraintreeCustomerIdKey, out var existingBraintreeCustomerId);

                        if (hasBraintreeCustomerId && !string.IsNullOrEmpty(existingBraintreeCustomerId))
                        {
                            var braintreeCustomer = await braintreeGateway.Customer.FindAsync(existingBraintreeCustomerId);

                            if (braintreeCustomer == null)
                            {
                                logger.LogError("Failed to retrieve Braintree customer ({BraintreeCustomerId}) when updating payment method for subscriber ({SubscriberID})", existingBraintreeCustomerId, subscriber.Id);

                                throw new BillingException();
                            }

                            await ReplaceBraintreePaymentMethodAsync(braintreeCustomer, token);

                            return;
                        }
                    }

                    var createdBraintreeCustomerId = await CreateBraintreeCustomerAsync(subscriber, token);

                    await AddBraintreeCustomerIdAsync(customer, createdBraintreeCustomerId);

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
        if (subscriber is not { GatewayCustomerId: not null })
        {
            logger.LogError("Cannot update tax information for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            throw new BillingException();
        }

        var customer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, new CustomerGetOptions
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
        if (subscriber is not { GatewayCustomerId: not null })
        {
            logger.LogError("Cannot verify bank account for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            throw new BillingException();
        }

        var setupIntentId = await setupIntentCache.Get(subscriber.Id);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            logger.LogError("No setup intent is cached to verify for subscriber ({SubscriberID})", subscriber.Id);

            throw new BillingException();
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

        throw new BillingException();
    }

    private async Task<PaymentSource?> GetPaymentSourceAsync(
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

        var attachedPaymentSource = PaymentSource.From(customer);

        if (attachedPaymentSource != null)
        {
            return attachedPaymentSource;
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

    private static TaxInformation? GetTaxInformation(
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
        var metadata = customer.Metadata ?? new Dictionary<string, string?>();

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

        var paymentMethods = await stripeAdapter.CustomerListPaymentMethodsAsync(customer.Id);

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
