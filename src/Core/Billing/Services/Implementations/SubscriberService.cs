// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;

using static Bit.Core.Billing.Utilities;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Services.Implementations;

using static StripeConstants;

public class SubscriberService(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ILogger<SubscriberService> logger,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    IProviderRepository providerRepository,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ITaxService taxService,
    IUserRepository userRepository) : ISubscriberService
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

        // Check if this is a Premium-to-Organization upgrade that can be reverted
        if (subscriber is Organization organization)
        {
            var canRevert = await CanRevertPremiumUpgradeAsync(subscription, organization);
            if (canRevert)
            {
                await RevertPremiumUpgradeAsync(subscription, organization);
                return;
            }
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
            if (subscription.Metadata.ContainsKey(StripeConstants.MetadataKeys.OrganizationId))
            {
                await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
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

            await stripeAdapter.CancelSubscriptionAsync(subscription.Id, options);
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

            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, options);
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
            PaymentMethodNonce = paymentMethodNonce
        });

        if (customerResult.IsSuccess())
        {
            return customerResult.Target.Id;
        }

        logger.LogError("Failed to create Braintree customer for subscriber ({ID})", subscriber.Id);

        throw new BillingException();
    }

#nullable enable
    public async Task<Customer> CreateStripeCustomer(ISubscriber subscriber)
    {
        if (!string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            throw new ConflictException("Subscriber already has a linked Stripe Customer");
        }

        var options = subscriber switch
        {
            Organization organization => new CustomerCreateOptions
            {
                Description = organization.DisplayBusinessName(),
                Email = organization.BillingEmail,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = organization.SubscriberType(),
                            Value = Max30Characters(organization.DisplayName())
                        }
                    ]
                },
                Metadata = new Dictionary<string, string>
                {
                    [MetadataKeys.OrganizationId] = organization.Id.ToString(),
                    [MetadataKeys.Region] = globalSettings.BaseServiceUri.CloudRegion
                }
            },
            Provider provider => new CustomerCreateOptions
            {
                Description = provider.DisplayBusinessName(),
                Email = provider.BillingEmail,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = provider.SubscriberType(),
                            Value = Max30Characters(provider.DisplayName())
                        }
                    ]
                },
                Metadata = new Dictionary<string, string>
                {
                    [MetadataKeys.ProviderId] = provider.Id.ToString(),
                    [MetadataKeys.Region] = globalSettings.BaseServiceUri.CloudRegion
                }
            },
            User user => new CustomerCreateOptions
            {
                Description = user.Name,
                Email = user.Email,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    CustomFields =
                    [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = user.SubscriberType(),
                            Value = Max30Characters(user.SubscriberName())
                        }
                    ]
                },
                Metadata = new Dictionary<string, string>
                {
                    [MetadataKeys.Region] = globalSettings.BaseServiceUri.CloudRegion,
                    [MetadataKeys.UserId] = user.Id.ToString()
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(subscriber))
        };

        var customer = await stripeAdapter.CreateCustomerAsync(options);

        switch (subscriber)
        {
            case Organization organization:
                organization.Gateway = GatewayType.Stripe;
                organization.GatewayCustomerId = customer.Id;
                await organizationRepository.ReplaceAsync(organization);
                break;
            case Provider provider:
                provider.Gateway = GatewayType.Stripe;
                provider.GatewayCustomerId = customer.Id;
                await providerRepository.ReplaceAsync(provider);
                break;
            case User user:
                user.Gateway = GatewayType.Stripe;
                user.GatewayCustomerId = customer.Id;
                await userRepository.ReplaceAsync(user);
                break;
        }

        return customer;

        string? Max30Characters(string? input)
            => input?.Length <= 30 ? input : input?[..30];
    }
#nullable disable

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
            var customer = await stripeAdapter.GetCustomerAsync(subscriber.GatewayCustomerId, customerGetOptions);

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
            var customer = await stripeAdapter.GetCustomerAsync(subscriber.GatewayCustomerId, customerGetOptions);

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
            var subscription = await stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId, subscriptionGetOptions);

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
            var subscription = await stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId, subscriptionGetOptions);

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
                            await stripeAdapter.DeleteBankAccountAsync(stripeCustomer.Id, source.Id);
                            break;
                        case Card:
                            await stripeAdapter.DeleteCardAsync(stripeCustomer.Id, source.Id);
                            break;
                    }
                }
            }

            var paymentMethods = stripeAdapter.ListPaymentMethodsAutoPagingAsync(new PaymentMethodListOptions
            {
                Customer = stripeCustomer.Id
            });

            await foreach (var paymentMethod in paymentMethods)
            {
                await stripeAdapter.DetachPaymentMethodAsync(paymentMethod.Id);
            }
        }
    }

    public async Task UpdatePaymentSource(
        ISubscriber subscriber,
        TokenizedPaymentSource tokenizedPaymentSource)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(tokenizedPaymentSource);

        var customerGetOptions = new CustomerGetOptions { Expand = ["tax", "tax_ids"] };
        var customer = await GetCustomerOrThrow(subscriber, customerGetOptions);

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
                    var getSetupIntentsForUpdatedPaymentMethod = stripeAdapter.ListSetupIntentsAsync(new SetupIntentListOptions
                    {
                        PaymentMethod = token
                    });

                    // Find the setup intent for the incoming payment method token.
                    var setupIntentsForUpdatedPaymentMethod = await getSetupIntentsForUpdatedPaymentMethod;

                    if (setupIntentsForUpdatedPaymentMethod.Count != 1)
                    {
                        logger.LogError("There were more than 1 setup intents for subscriber's ({SubscriberID}) updated payment method", subscriber.Id);

                        throw new BillingException();
                    }

                    var matchingSetupIntent = setupIntentsForUpdatedPaymentMethod.First();

                    // Store the incoming payment method's setup intent ID in the cache for the subscriber so it can be verified later.
                    await setupIntentCache.Set(subscriber.Id, matchingSetupIntent.Id);

                    // Remove the customer's other attached Stripe payment methods.
                    var postProcessing = new List<Task>
                    {
                        RemoveStripePaymentMethodsAsync(customer),
                        RemoveBraintreeCustomerIdAsync(customer)
                    };

                    await Task.WhenAll(postProcessing);

                    break;
                }
            case PaymentMethodType.Card:
                {
                    // Remove the customer's other attached Stripe payment methods.
                    await RemoveStripePaymentMethodsAsync(customer);

                    // Attach the incoming payment method.
                    await stripeAdapter.AttachPaymentMethodAsync(token,
                        new PaymentMethodAttachOptions { Customer = subscriber.GatewayCustomerId });

                    var metadata = customer.Metadata;

                    if (metadata.TryGetValue(BraintreeCustomerIdKey, out var value))
                    {
                        metadata[BraintreeCustomerIdOldKey] = value;
                        metadata[BraintreeCustomerIdKey] = null;
                    }

                    // Set the customer's default payment method in Stripe and remove their Braintree customer ID.
                    await stripeAdapter.UpdateCustomerAsync(subscriber.GatewayCustomerId, new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = token
                        },
                        Metadata = metadata
                    });

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

        customer = await stripeAdapter.UpdateCustomerAsync(customer.Id, new CustomerUpdateOptions
        {
            Address = new AddressOptions
            {
                Country = taxInformation.Country,
                PostalCode = taxInformation.PostalCode,
                Line1 = taxInformation.Line1 ?? string.Empty,
                Line2 = taxInformation.Line2,
                City = taxInformation.City,
                State = taxInformation.State
            },
            Expand = ["subscriptions", "tax", "tax_ids"]
        });

        var taxId = customer.TaxIds?.FirstOrDefault();

        if (taxId != null)
        {
            await stripeAdapter.DeleteTaxIdAsync(customer.Id, taxId.Id);
        }

        if (!string.IsNullOrWhiteSpace(taxInformation.TaxId))
        {
            var taxIdType = taxInformation.TaxIdType;
            if (string.IsNullOrWhiteSpace(taxIdType))
            {
                taxIdType = taxService.GetStripeTaxCode(taxInformation.Country,
                    taxInformation.TaxId);

                if (taxIdType == null)
                {
                    logger.LogWarning("Could not infer tax ID type in country '{Country}' with tax ID '{TaxID}'.",
                        taxInformation.Country,
                        taxInformation.TaxId);

                    throw new BadRequestException("billingTaxIdTypeInferenceError");
                }
            }

            try
            {
                await stripeAdapter.CreateTaxIdAsync(customer.Id,
                    new TaxIdCreateOptions { Type = taxIdType, Value = taxInformation.TaxId });

                if (taxIdType == StripeConstants.TaxIdType.SpanishNIF)
                {
                    await stripeAdapter.CreateTaxIdAsync(customer.Id,
                        new TaxIdCreateOptions { Type = StripeConstants.TaxIdType.EUVAT, Value = $"ES{taxInformation.TaxId}" });
                }
            }
            catch (StripeException e)
            {
                switch (e.StripeError.Code)
                {
                    case StripeConstants.ErrorCodes.TaxIdInvalid:
                        logger.LogWarning("Invalid tax ID '{TaxID}' for country '{Country}'.",
                            taxInformation.TaxId,
                            taxInformation.Country);

                        throw new BadRequestException("billingInvalidTaxIdError");

                    default:
                        logger.LogError(e,
                            "Error creating tax ID '{TaxId}' in country '{Country}' for customer '{CustomerID}'.",
                            taxInformation.TaxId,
                            taxInformation.Country,
                            customer.Id);

                        throw new BadRequestException("billingTaxIdCreationError");
                }
            }
        }

        var subscription =
            customer.Subscriptions.First(subscription => subscription.Id == subscriber.GatewaySubscriptionId);

        var isBusinessUseSubscriber = subscriber switch
        {
            Organization organization => organization.PlanType.GetProductTier() is not ProductTierType.Free and not ProductTierType.Families,
            Provider => true,
            _ => false
        };

        if (isBusinessUseSubscriber)
        {
            switch (customer)
            {
                case
                {
                    Address.Country: not Core.Constants.CountryAbbreviations.UnitedStates,
                    TaxExempt: not TaxExempt.Reverse
                }:
                    await stripeAdapter.UpdateCustomerAsync(customer.Id,
                        new CustomerUpdateOptions { TaxExempt = TaxExempt.Reverse });
                    break;
                case
                {
                    Address.Country: Core.Constants.CountryAbbreviations.UnitedStates,
                    TaxExempt: TaxExempt.Reverse
                }:
                    await stripeAdapter.UpdateCustomerAsync(customer.Id,
                        new CustomerUpdateOptions { TaxExempt = TaxExempt.None });
                    break;
            }

            if (!subscription.AutomaticTax.Enabled)
            {
                await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
        }
        else
        {
            var automaticTaxShouldBeEnabled = subscriber switch
            {
                User => true,
                Organization organization => organization.PlanType.GetProductTier() == ProductTierType.Families ||
                                             customer.Address.Country == Core.Constants.CountryAbbreviations.UnitedStates || (customer.TaxIds?.Any() ?? false),
                Provider => customer.Address.Country == Core.Constants.CountryAbbreviations.UnitedStates || (customer.TaxIds?.Any() ?? false),
                _ => false
            };

            if (automaticTaxShouldBeEnabled && !subscription.AutomaticTax.Enabled)
            {
                await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
        }
    }

    public async Task<bool> IsValidGatewayCustomerIdAsync(ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        if (string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            // subscribers are allowed to have no customer id as a business rule
            return true;
        }
        try
        {
            await stripeAdapter.GetCustomerAsync(subscriber.GatewayCustomerId);
            return true;
        }
        catch (StripeException e) when (e.StripeError.Code == "resource_missing")
        {
            return false;
        }
    }

    public async Task<bool> IsValidGatewaySubscriptionIdAsync(ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            // subscribers are allowed to have no subscription id as a business rule
            return true;
        }
        try
        {
            await stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId);
            return true;
        }
        catch (StripeException e) when (e.StripeError.Code == "resource_missing")
        {
            return false;
        }
    }

    #region Shared Utilities

    private async Task AddBraintreeCustomerIdAsync(
        Customer customer,
        string braintreeCustomerId)
    {
        var metadata = customer.Metadata ?? new Dictionary<string, string>();

        metadata[BraintreeCustomerIdKey] = braintreeCustomerId;

        await stripeAdapter.UpdateCustomerAsync(customer.Id, new CustomerUpdateOptions
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
        var setupIntentId = await setupIntentCache.GetSetupIntentIdForSubscriber(subscriberId);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            return null;
        }

        var setupIntent = await stripeAdapter.GetSetupIntentAsync(setupIntentId, new SetupIntentGetOptions
        {
            Expand = ["payment_method"]
        });

        return PaymentSource.From(setupIntent);
    }

    private async Task RemoveBraintreeCustomerIdAsync(
        Customer customer)
    {
        var metadata = customer.Metadata ?? new Dictionary<string, string>();

        if (metadata.TryGetValue(BraintreeCustomerIdKey, out var value))
        {
            metadata[BraintreeCustomerIdOldKey] = value;
            metadata[BraintreeCustomerIdKey] = null;

            await stripeAdapter.UpdateCustomerAsync(customer.Id, new CustomerUpdateOptions
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
                        await stripeAdapter.DeleteBankAccountAsync(customer.Id, source.Id);
                        break;
                    case Card:
                        await stripeAdapter.DeleteCardAsync(customer.Id, source.Id);
                        break;
                }
            }
        }

        var paymentMethods = await stripeAdapter.ListCustomerPaymentMethodsAsync(customer.Id);

        await Task.WhenAll(paymentMethods.Select(pm => stripeAdapter.DetachPaymentMethodAsync(pm.Id)));
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

    /// <summary>
    /// Checks if a Premium-to-Organization upgrade can be reverted by validating the subscription metadata
    /// and organization match.
    /// </summary>
    /// <param name="subscription">The Stripe subscription to check</param>
    /// <param name="organization">The organization attempting to cancel</param>
    /// <returns>True if the reversion is possible, false otherwise</returns>
    private Task<bool> CanRevertPremiumUpgradeAsync(Subscription subscription, Organization organization)
    {
        // Extract all metadata once
        var metadata = subscription.Metadata;

        // Check if subscription has the premium upgrade metadata
        if (!metadata.TryGetValue(MetadataKeys.PreviousPremiumPriceId, out _) ||
            !metadata.TryGetValue(MetadataKeys.UpgradedOrganizationId, out var upgradedOrganizationIdStr) ||
            !metadata.TryGetValue(MetadataKeys.PreviousPremiumUserId, out _))
        {
            logger.LogDebug("Subscription {SubscriptionId} does not have premium upgrade metadata", subscription.Id);
            return Task.FromResult(false);
        }

        // Parse IDs once
        if (!Guid.TryParse(upgradedOrganizationIdStr, out var upgradedOrganizationId))
        {
            logger.LogWarning("Invalid GUID format in premium upgrade metadata for subscription {SubscriptionId}", subscription.Id);
            return Task.FromResult(false);
        }

        // Verify that this is the organization that was upgraded
        if (upgradedOrganizationId != organization.Id)
        {
            logger.LogWarning(
                "Organization {OrganizationId} attempted to revert subscription {SubscriptionId} that belongs to organization {ActualOrganizationId}",
                organization.Id, subscription.Id, upgradedOrganizationId);
            return Task.FromResult(false);
        }

        // Verify subscription is in trial - reversion only allowed during trial period
        if (subscription.Status != SubscriptionStatus.Trialing)
        {
            logger.LogWarning(
                "Cannot revert subscription {SubscriptionId} - not in trial period (current status: {Status})",
                subscription.Id, subscription.Status);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Reverts a Premium-to-Organization upgrade by restoring the subscription to the original Premium plan.
    /// This method should only be called after CanRevertPremiumUpgradeAsync returns true.
    /// </summary>
    /// <param name="subscription">The Stripe subscription to revert</param>
    /// <param name="organization">The organization whose subscription is being reverted</param>
    /// <exception cref="BillingException">Thrown if the reversion fails</exception>
    private async Task RevertPremiumUpgradeAsync(Subscription subscription, Organization organization)
    {
        var metadata = subscription.Metadata;

        // Extract required metadata values (we know they exist from CanRevertPremiumUpgradeAsync)
        var previousPremiumPriceId = metadata[MetadataKeys.PreviousPremiumPriceId];
        var previousPremiumUserIdStr = metadata[MetadataKeys.PreviousPremiumUserId];

        if (!Guid.TryParse(previousPremiumUserIdStr, out var previousPremiumUserId))
        {
            logger.LogError("Invalid GUID format for previous premium user ID in subscription {SubscriptionId}", subscription.Id);
            throw new BillingException("Invalid premium upgrade metadata");
        }

        try
        {
            logger.LogInformation("Reverting Premium-to-Organization upgrade for organization {OrganizationId}", organization.Id);

            // STEP 1: Validate user exists BEFORE making any Stripe changes
            var user = await userRepository.GetByIdAsync(previousPremiumUserId);
            if (user == null)
            {
                logger.LogError("Cannot revert subscription - user {UserId} not found", previousPremiumUserId);
                throw new BillingException(message: "Cannot revert subscription - original Premium user not found");
            }

            // STEP 2: Get the current Premium plan details from pricing client
            // Note: GetAvailablePremiumPlan() will throw NotFoundException if no Premium plan exists
            var premiumPlan = await pricingClient.GetAvailablePremiumPlan();
            if (!premiumPlan.Available)
            {
                logger.LogError("Cannot revert subscription - Premium plan exists but is not available");
                throw new BillingException("Premium plan is not currently available");
            }

            // STEP 3: Parse additional metadata
            // Restore the user's original Premium expiration date from before they upgraded to Organization
            DateTime? periodEnd = null;
            if (metadata.TryGetValue(MetadataKeys.PreviousPeriodEndDate, out var periodEndStr) &&
                DateTime.TryParse(periodEndStr, out var parsedPeriodEnd))
            {
                periodEnd = parsedPeriodEnd;
            }

            int additionalStorageGb = 0;
            if (metadata.TryGetValue(MetadataKeys.PreviousAdditionalStorage, out var storageStr) &&
                int.TryParse(storageStr, out var storage))
            {
                additionalStorageGb = storage;
            }

            // Get previous storage price ID (if available) or fallback to current
            string storagePriceId = premiumPlan.Storage.StripePriceId; // Default to current
            if (metadata.TryGetValue(MetadataKeys.PreviousStoragePriceId, out var previousStoragePriceId))
            {
                storagePriceId = previousStoragePriceId; // Use historical price if available
            }

            // STEP 4: Build subscription items - delete all existing organization items and add Premium items
            var subscriptionItemOptions = new List<SubscriptionItemOptions>();

            // Delete all existing organization subscription items
            foreach (var existingItem in subscription.Items.Data)
            {
                subscriptionItemOptions.Add(new SubscriptionItemOptions
                {
                    Id = existingItem.Id,
                    Deleted = true
                });
            }

            // Add Premium seat item - use the original price from metadata
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = previousPremiumPriceId,
                Quantity = 1
            });

            // Add storage item if user had additional storage
            if (additionalStorageGb > 0)
            {
                subscriptionItemOptions.Add(new SubscriptionItemOptions
                {
                    Price = storagePriceId,
                    Quantity = additionalStorageGb
                });

                logger.LogInformation("Restoring {StorageGb}GB additional storage for user {UserId}", additionalStorageGb, previousPremiumUserId);
            }

            // STEP 5: Preserve important metadata and remove premium upgrade metadata
            var updatedMetadata = new Dictionary<string, string>(metadata);
            updatedMetadata.Remove(MetadataKeys.PreviousPremiumPriceId);
            updatedMetadata.Remove(MetadataKeys.PreviousPeriodEndDate);
            updatedMetadata.Remove(MetadataKeys.UpgradedOrganizationId);
            updatedMetadata.Remove(MetadataKeys.PreviousPremiumUserId);
            updatedMetadata.Remove(MetadataKeys.PreviousAdditionalStorage);
            updatedMetadata.Remove(MetadataKeys.PreviousStoragePriceId);

            // Ensure user ID is in metadata
            updatedMetadata[MetadataKeys.UserId] = previousPremiumUserId.ToString();
            updatedMetadata.Remove(MetadataKeys.OrganizationId);

            // STEP 6: Update Stripe subscription
            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
            {
                Items = subscriptionItemOptions,
                TrialEnd = periodEnd,
                Metadata = updatedMetadata
            });

            // STEP 7: Update user's Premium status and storage
            user.Premium = true;
            user.PremiumExpirationDate = periodEnd;
            user.Gateway = GatewayType.Stripe;
            user.GatewaySubscriptionId = subscription.Id;
            user.GatewayCustomerId = subscription.CustomerId; // Ensure user's customer ID matches subscription
            user.MaxStorageGb = (short)(premiumPlan.Storage.Provided + additionalStorageGb);

            await userRepository.ReplaceAsync(user);

            // STEP 8: Clear organization's gateway references and disable it
            // NOTE: The organization entity itself is NOT deleted. It remains in the database with:
            // - GatewayCustomerId set to null (no gateway customer)
            // - GatewaySubscriptionId set to null (no active subscription)
            // - Enabled set to false (organization is disabled)
            // - All existing data (vaults, members, etc.) preserved
            organization.GatewayCustomerId = null;
            organization.GatewaySubscriptionId = null;
            organization.Enabled = false;
            await organizationRepository.ReplaceAsync(organization);

            logger.LogInformation(
                "Successfully reverted Premium-to-Organization upgrade for organization {OrganizationId}. Subscription {SubscriptionId} restored to user {UserId}",
                organization.Id, subscription.Id, previousPremiumUserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revert Premium-to-Organization upgrade for organization {OrganizationId}", organization.Id);
            throw new BillingException(
                message: "Failed to revert subscription upgrade",
                innerException: ex);
        }
    }

    #endregion
}
