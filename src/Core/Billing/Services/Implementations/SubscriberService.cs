// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
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

using static StripeConstants;

public class SubscriberService(
    IBraintreeGateway braintreeGateway,
    IFeatureService featureService,
    IGlobalSettings globalSettings,
    ILogger<SubscriberService> logger,
    IOrganizationRepository organizationRepository,
    IPriceIncreaseScheduler priceIncreaseScheduler,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    IUserRepository userRepository) : ISubscriberService
{
    public async Task CancelSubscription(
        ISubscriber subscriber,
        bool cancelImmediately,
        OffboardingSurveyResponse offboardingSurveyResponse = null)
    {
        var subscription = await GetSubscriptionOrThrow(subscriber,
            new SubscriptionGetOptions { Expand = ["test_clock"] });

        if (subscription.CanceledAt.HasValue ||
            subscription.Status == "canceled" ||
            subscription.Status == "unpaid" ||
            subscription.Status == "incomplete_expired")
        {
            logger.LogWarning("Cannot cancel subscription ({ID}) that's already inactive", subscription.Id);

            throw new BillingException();
        }

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

        // Build once from survey — null when survey is absent (system-initiated cancellation)
        var cancellationDetails = offboardingSurveyResponse != null
            ? new SubscriptionCancellationDetailsOptions
            {
                Comment = offboardingSurveyResponse.Feedback,
                Feedback = validCancellationReasons.Contains(offboardingSurveyResponse.Reason)
                    ? offboardingSurveyResponse.Reason
                    : null
            }
            : null;

        var cancellingUserMetadata = offboardingSurveyResponse != null
            ? new Dictionary<string, string>
            {
                { "cancellingUserId", offboardingSurveyResponse.UserId.ToString() }
            }
            : null;

        if (cancelImmediately)
        {
            await CancelSubscriptionImmediatelyAsync(subscription, cancellationDetails, cancellingUserMetadata);
        }
        else
        {
            await CancelSubscriptionAtPeriodEndAsync(subscription, cancellationDetails, cancellingUserMetadata);
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

    private async Task CancelSubscriptionImmediatelyAsync(
        Subscription subscription,
        SubscriptionCancellationDetailsOptions? cancellationDetails,
        Dictionary<string, string>? cancellingUserMetadata)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            var activeSchedule = await GetActiveScheduleAsync(subscription);
            if (activeSchedule != null)
            {
                await priceIncreaseScheduler.Release(subscription.CustomerId, subscription.Id);
            }
        }

        if (cancellingUserMetadata != null && subscription.Metadata.ContainsKey(MetadataKeys.OrganizationId))
        {
            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                new SubscriptionUpdateOptions { Metadata = cancellingUserMetadata });
        }

        var cancelOptions = new SubscriptionCancelOptions
        {
            CancellationDetails = cancellationDetails
        };

        await stripeAdapter.CancelSubscriptionAsync(subscription.Id, cancelOptions);
    }

    private async Task CancelSubscriptionAtPeriodEndAsync(
        Subscription subscription,
        SubscriptionCancellationDetailsOptions? cancellationDetails,
        Dictionary<string, string>? cancellingUserMetadata)
    {
        var updateOptions = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true,
            CancellationDetails = cancellationDetails,
            Metadata = cancellingUserMetadata
        };

        if (featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            var activeSchedule = await GetActiveScheduleAsync(subscription);

            if (activeSchedule is { Phases.Count: > 0 })
            {
                logger.LogInformation(
                    "{Service}: Active subscription schedule ({ScheduleId}) found for subscription ({SubscriptionId}), releasing schedule before cancellation",
                    GetType().Name, activeSchedule.Id, subscription.Id);

                await stripeAdapter.ReleaseSubscriptionScheduleAsync(activeSchedule.Id);

                updateOptions.Metadata = new Dictionary<string, string>(cancellingUserMetadata ?? [])
                {
                    [MetadataKeys.CancelledDuringDeferredPriceIncrease] = "true"
                };
            }
        }

        await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, updateOptions);
    }

    private async Task<SubscriptionSchedule?> GetActiveScheduleAsync(Subscription subscription)
    {
        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

        return schedules.Data.FirstOrDefault(s =>
            s.SubscriptionId == subscription.Id &&
            s.Status == SubscriptionScheduleStatus.Active);
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

        return await GetPaymentSourceAsync(customer);
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

    private async Task<PaymentSource> GetPaymentSourceAsync(Customer customer)
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
         * Query Stripe for SetupIntents associated with this customer.
         */
        var setupIntents = await stripeAdapter.ListSetupIntentsAsync(new SetupIntentListOptions
        {
            Customer = customer.Id,
            Expand = ["data.payment_method"]
        });

        var unverifiedBankAccount = setupIntents?.FirstOrDefault(si => si.IsUnverifiedBankAccount());

        return unverifiedBankAccount != null ? PaymentSource.From(unverifiedBankAccount) : null;
    }

    #endregion
}
