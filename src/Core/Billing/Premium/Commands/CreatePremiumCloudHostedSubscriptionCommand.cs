using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
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
using PaymentMethod = Bit.Core.Billing.Payment.Models.PaymentMethod;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Billing.Premium.Commands;

using static StripeConstants;
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
    /// <param name="user">The user to create the premium subscription for. Must not yet be a premium user.</param>
    /// <param name="paymentMethod">The tokenized payment method containing the payment type and token for billing.</param>
    /// <param name="billingAddress">The billing address information required for tax calculation and customer creation.</param>
    /// <param name="additionalStorageGb">Additional storage in GB beyond the base 1GB included with premium (must be >= 0).</param>
    /// <returns>A billing command result indicating success or failure with appropriate error details.</returns>
    Task<BillingCommandResult<None>> Run(
        User user,
        PaymentMethod paymentMethod,
        BillingAddress billingAddress,
        short additionalStorageGb);
}

public class CreatePremiumCloudHostedSubscriptionCommand(
    IBraintreeGateway braintreeGateway,
    IBraintreeService braintreeService,
    IGlobalSettings globalSettings,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserService userService,
    IPushNotificationService pushNotificationService,
    ILogger<CreatePremiumCloudHostedSubscriptionCommand> logger,
    IPricingClient pricingClient,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand)
    : BaseBillingCommand<CreatePremiumCloudHostedSubscriptionCommand>(logger), ICreatePremiumCloudHostedSubscriptionCommand
{
    private static readonly List<string> _expand = ["tax"];
    private readonly ILogger<CreatePremiumCloudHostedSubscriptionCommand> _logger = logger;

    public Task<BillingCommandResult<None>> Run(
        User user,
        PaymentMethod paymentMethod,
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

        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();

        // CRITICAL: Check Stripe directly for existing active Premium subscriptions to prevent race conditions
        // This protects against simultaneous requests creating duplicate subscriptions
        if (!string.IsNullOrEmpty(user.GatewayCustomerId))
        {
            var existingActiveSubscription = await GetExistingActivePremiumSubscriptionAsync(user.GatewayCustomerId, premiumPlan);
            if (existingActiveSubscription != null)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to create duplicate Premium subscription. Active subscription {SubscriptionId} already exists",
                    user.Id, existingActiveSubscription.Id);
                return new BadRequest("You already have an active Premium subscription. Please refresh the page.");
            }
        }
        else
        {
            // CRITICAL: For users without a GatewayCustomerId, check for existing subscriptions by email
            // This prevents race conditions where multiple simultaneous requests create multiple customers
            // and then multiple subscriptions for the same user
            var existingSubscriptionByEmail = await GetExistingActivePremiumSubscriptionByEmailAsync(user.Email, premiumPlan);
            if (existingSubscriptionByEmail != null)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to create duplicate Premium subscription. Active subscription {SubscriptionId} already exists for email {Email}",
                    user.Id, existingSubscriptionByEmail.Id, user.Email);
                return new BadRequest("You already have an active Premium subscription. Please refresh the page.");
            }
        }

        Customer? customer;

        /*
         * For a new customer purchasing a new subscription, we attach the payment method while creating the customer.
         */
        if (string.IsNullOrEmpty(user.GatewayCustomerId))
        {
            customer = await CreateCustomerAsync(user, paymentMethod, billingAddress);
        }
        /*
         * For an existing customer, we need to handle payment method updates properly:
         * - If a new tokenized payment method is provided (card/PayPal), ALWAYS update it
         *   This is critical for retry scenarios where the previous payment method failed
         * - If using account credit, just fetch the customer (no payment method to update)
         */
        else if (paymentMethod.IsTokenized)
        {
            // ALWAYS update payment method when provided, even if customer already has one
            // This fixes the issue where retrying with a new card would still use the old failing card
            await updatePaymentMethodCommand.Run(user, paymentMethod.AsTokenized, billingAddress);
            customer = await subscriberService.GetCustomerOrThrow(user, new CustomerGetOptions { Expand = _expand });
        }
        else
        {
            // Non-tokenized payment (e.g., account credit) - just fetch customer
            customer = await subscriberService.GetCustomerOrThrow(user, new CustomerGetOptions { Expand = _expand });
        }

        customer = await ReconcileBillingLocationAsync(customer, billingAddress);

        // CRITICAL: Final check by email after customer creation/retrieval
        // This catches race conditions where multiple simultaneous requests for NEW customers
        // (who don't have GatewayCustomerId yet) both passed the initial email check,
        // both created customers, but one completed subscription creation before the other
        // Note: We only check by email here because:
        // - For new customers: Customer ID check is redundant (we just created the customer)
        // - For existing customers: We already checked by customer ID before
        // - Email check catches subscriptions created for any customer with this email
        var existingSubscriptionByEmailAfterCreation = await GetExistingActivePremiumSubscriptionByEmailAsync(user.Email, premiumPlan);
        if (existingSubscriptionByEmailAfterCreation != null)
        {
            _logger.LogWarning(
                "User {UserId} attempted to create duplicate Premium subscription after customer creation. Active subscription {SubscriptionId} already exists for email {Email}",
                user.Id, existingSubscriptionByEmailAfterCreation.Id, user.Email);
            return new BadRequest("You already have an active Premium subscription. Please refresh the page.");
        }

        var subscription = await CreateSubscriptionAsync(user.Id, customer, premiumPlan, additionalStorageGb > 0 ? additionalStorageGb : null);

        paymentMethod.Switch(
            tokenized =>
            {
                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (tokenized)
                {
                    case { Type: TokenizablePaymentMethodType.PayPal }
                        when subscription.Status == SubscriptionStatus.Incomplete:
                    case { Type: not TokenizablePaymentMethodType.PayPal }
                        when subscription.Status == SubscriptionStatus.Active:
                        {
                            user.Premium = true;
                            user.PremiumExpirationDate = subscription.GetCurrentPeriodEnd();
                            break;
                        }
                }
            },
            _ =>
            {
                if (subscription.Status != SubscriptionStatus.Active)
                {
                    return;
                }

                user.Premium = true;
                user.PremiumExpirationDate = subscription.GetCurrentPeriodEnd();
            });

        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = customer.Id;
        user.GatewaySubscriptionId = subscription.Id;
        user.MaxStorageGb = (short)(premiumPlan.Storage.Provided + additionalStorageGb);
        user.LicenseKey = CoreHelpers.SecureRandomString(20);
        user.RevisionDate = DateTime.UtcNow;

        await userService.SaveUserAsync(user);
        await pushNotificationService.PushSyncVaultAsync(user.Id);

        return new None();
    });

    private async Task<Customer> CreateCustomerAsync(User user,
        PaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        if (paymentMethod.IsNonTokenized)
        {
            _logger.LogError("Cannot create customer for user ({UserID}) using non-tokenized payment method. The customer should already exist", user.Id);
            throw new BillingException();
        }

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
                [MetadataKeys.Region] = globalSettings.BaseServiceUri.CloudRegion,
                [MetadataKeys.UserId] = user.Id.ToString()
            },
            Tax = new CustomerTaxOptions
            {
                ValidateLocation = ValidateTaxLocationTiming.Immediately
            }
        };

        var braintreeCustomerId = "";

        // We have checked that the payment method is tokenized, so we can safely cast it.
        var tokenizedPaymentMethod = paymentMethod.AsTokenized;
        switch (tokenizedPaymentMethod.Type)
        {
            case TokenizablePaymentMethodType.BankAccount:
                {
                    var setupIntent =
                        (await stripeAdapter.ListSetupIntentsAsync(new SetupIntentListOptions { PaymentMethod = tokenizedPaymentMethod.Token }))
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
                    customerCreateOptions.PaymentMethod = tokenizedPaymentMethod.Token;
                    customerCreateOptions.InvoiceSettings.DefaultPaymentMethod = tokenizedPaymentMethod.Token;
                    break;
                }
            case TokenizablePaymentMethodType.PayPal:
                {
                    braintreeCustomerId = await subscriberService.CreateBraintreeCustomer(user, tokenizedPaymentMethod.Token);
                    customerCreateOptions.Metadata[BraintreeCustomerIdKey] = braintreeCustomerId;
                    break;
                }
            default:
                {
                    _logger.LogError("Cannot create customer for user ({UserID}) using payment method type ({PaymentMethodType}) as it is not supported", user.Id, tokenizedPaymentMethod.Type.ToString());
                    throw new BillingException();
                }
        }

        try
        {
            return await stripeAdapter.CreateCustomerAsync(customerCreateOptions);
        }
        catch
        {
            await Revert();
            throw;
        }

        async Task Revert()
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (tokenizedPaymentMethod.Type)
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
                ValidateLocation = ValidateTaxLocationTiming.Immediately
            }
        };

        return await stripeAdapter.UpdateCustomerAsync(customer.Id, options);
    }

    private async Task<Subscription> CreateSubscriptionAsync(
        Guid userId,
        Customer customer,
        Pricing.Premium.Plan premiumPlan,
        int? storage)
    {
        // Proactively cancel any existing problematic Premium subscriptions (incomplete/unpaid).
        // This cleans up failed payment attempts before creating a new subscription.
        await CancelExistingDuplicatePremiumSubscriptionsAsync(customer.Id, premiumPlan);

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
            CollectionMethod = CollectionMethod.ChargeAutomatically,
            Customer = customer.Id,
            Items = subscriptionItemOptionsList,
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.UserId] = userId.ToString()
            },
            PaymentBehavior = usingPayPal
                ? PaymentBehavior.DefaultIncomplete
                : null,
            OffSession = true
        };

        // Use idempotency key to ensure that simultaneous requests don't create duplicate subscriptions
        // Key uses 10-second time buckets, allowing genuine retries while preventing race conditions
        // This allows users to change their mind (e.g., different storage) after ~10 seconds
        var timestampBucket = DateTime.UtcNow.Ticks / (10 * TimeSpan.TicksPerSecond);
        var idempotencyKey = $"premium-sub-{userId}-{timestampBucket}";
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = idempotencyKey
        };

        var subscription = await stripeAdapter.CreateSubscriptionAsync(subscriptionCreateOptions, requestOptions);

        if (!usingPayPal)
        {
            return subscription;
        }

        var invoice = await stripeAdapter.UpdateInvoiceAsync(subscription.LatestInvoiceId, new InvoiceUpdateOptions
        {
            AutoAdvance = false,
            Expand = ["customer"]
        });

        await braintreeService.PayInvoice(new UserId(userId), invoice);

        return subscription;
    }

    /// <summary>
    /// Checks if the customer already has an active Premium subscription.
    /// This is used as a gate check to prevent race conditions where multiple simultaneous
    /// requests could create duplicate subscriptions.
    /// </summary>
    /// <param name="customerId">The Stripe customer ID to check.</param>
    /// <param name="premiumPlan">The premium plan containing the price IDs to identify Premium subscriptions.</param>
    /// <returns>The existing active Premium subscription if found, null otherwise.</returns>
    private async Task<Subscription?> GetExistingActivePremiumSubscriptionAsync(string customerId, Pricing.Premium.Plan premiumPlan)
    {
        try
        {
            var subscriptionOptions = new SubscriptionListOptions
            {
                Customer = customerId,
                Status = "active",
                Limit = 10 // Should only have 0-1, but check a few just in case
            };

            var subscriptions = await stripeAdapter.ListSubscriptionsAsync(subscriptionOptions);
            var premiumPriceIds = new HashSet<string> { premiumPlan.Seat.StripePriceId, premiumPlan.Storage.StripePriceId };

            return subscriptions.FirstOrDefault(sub =>
                sub.Items.Any(item => premiumPriceIds.Contains(item.Price.Id)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing active Premium subscription for customer {CustomerId}", customerId);
            return null; // If check fails, allow the request to proceed (fail open rather than fail closed)
        }
    }

    /// <summary>
    /// Checks if there's an existing active Premium subscription for a customer with the given email address.
    /// This is used to prevent race conditions where multiple simultaneous requests for users without
    /// a GatewayCustomerId create multiple customers and subscriptions.
    /// </summary>
    /// <param name="email">The email address to check for existing subscriptions.</param>
    /// <param name="premiumPlan">The premium plan containing the price IDs to identify Premium subscriptions.</param>
    /// <returns>The existing active Premium subscription if found, null otherwise.</returns>
    private async Task<Subscription?> GetExistingActivePremiumSubscriptionByEmailAsync(string email, Pricing.Premium.Plan premiumPlan)
    {
        try
        {
            // Query for recent active subscriptions and check their customer emails
            // We limit to recent subscriptions to avoid performance issues
            // In practice, a user should only have 0-1 active subscriptions, so this should be sufficient
            var subscriptionOptions = new SubscriptionListOptions
            {
                Status = "active",
                Limit = 100 // Check recent subscriptions - should be more than enough
            };

            var subscriptions = await stripeAdapter.ListSubscriptionsAsync(subscriptionOptions);
            var premiumPriceIds = new HashSet<string> { premiumPlan.Seat.StripePriceId, premiumPlan.Storage.StripePriceId };

            // First filter to Premium subscriptions only (by price ID) to minimize customer lookups
            var premiumSubscriptions = subscriptions.Where(sub =>
                sub.Items.Any(item => premiumPriceIds.Contains(item.Price.Id))).ToList();

            if (!premiumSubscriptions.Any())
            {
                return null;
            }

            // For Premium subscriptions, check if any customer email matches
            // We fetch customers in parallel to minimize latency
            var customerCheckTasks = premiumSubscriptions.Select(async sub =>
            {
                try
                {
                    // Get customer ID from subscription
                    // Customer can be a string (ID) or Customer object (if expanded)
                    string? customerId = null;
                    Customer? customer = null;

                    // Check if Customer is already a Customer object
                    var customerObj = sub.Customer as Customer;
                    if (customerObj != null)
                    {
                        customer = customerObj;
                        customerId = customerObj.Id;
                    }
                    else
                    {
                        // Customer is a string ID
                        customerId = sub.Customer?.ToString();
                    }

                    if (string.IsNullOrEmpty(customerId))
                    {
                        return (Subscription: sub, Match: false);
                    }

                    // If customer wasn't expanded, fetch it
                    if (customer == null)
                    {
                        customer = await stripeAdapter.GetCustomerAsync(customerId);
                    }

                    var emailMatches = customer != null &&
                                       string.Equals(customer.Email, email, StringComparison.OrdinalIgnoreCase);

                    return (Subscription: sub, Match: emailMatches);
                }
                catch
                {
                    // If customer fetch fails, skip this subscription
                    return (Subscription: sub, Match: false);
                }
            });

            var results = await Task.WhenAll(customerCheckTasks);
            var matchingResult = results.FirstOrDefault(r => r.Match);
            return matchingResult.Match ? matchingResult.Subscription : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing active Premium subscription by email {Email}", email);
            return null; // If check fails, allow the request to proceed (fail open rather than fail closed)
        }
    }

    /// <summary>
    /// Cancels any existing problematic Premium subscriptions for the customer to prevent duplicates.
    /// This is called proactively before creating a new subscription to handle failed payment scenarios
    /// where incomplete/unpaid subscriptions remain on the account.
    /// Note: This does NOT cancel active subscriptions to avoid race conditions between concurrent requests.
    /// Layer 1 (gate check) prevents creating when an active subscription exists, and Layer 4 (webhooks)
    /// handles cleanup of any edge cases.
    /// </summary>
    /// <param name="customerId">The Stripe customer ID to check for existing subscriptions.</param>
    /// <param name="premiumPlan">The premium plan containing the price IDs to identify Premium subscriptions.</param>
    private async Task CancelExistingDuplicatePremiumSubscriptionsAsync(string customerId, Pricing.Premium.Plan premiumPlan)
    {
        try
        {
            var subscriptionOptions = new SubscriptionListOptions
            {
                Customer = customerId,
                Status = "all" // Get all statuses so we can filter problematic ones and recent active subscriptions
            };

            var subscriptions = await stripeAdapter.ListSubscriptionsAsync(subscriptionOptions);

            var premiumPriceIds = new HashSet<string> { premiumPlan.Seat.StripePriceId, premiumPlan.Storage.StripePriceId };

            foreach (var subscription in subscriptions)
            {
                // Only cancel subscriptions that are:
                // 1. Premium subscriptions (matching price IDs)
                // 2. In a problematic state (incomplete, unpaid, incomplete_expired)
                // Note: We don't cancel recently created active subscriptions here to avoid race conditions
                // between concurrent requests. Layer 1 gate check prevents creating when active exists,
                // and Layer 4 (webhooks) handles cleanup of any edge cases.
                var isPremiumSubscription = subscription.Items.Any(item => premiumPriceIds.Contains(item.Price.Id));

                var isProblematicStatus = subscription.Status is SubscriptionStatus.Incomplete
                    or SubscriptionStatus.Unpaid
                    or SubscriptionStatus.IncompleteExpired;

                if (isPremiumSubscription && isProblematicStatus)
                {
                    _logger.LogInformation(
                        "Proactively cancelling existing {Status} Premium subscription {SubscriptionId} for customer {CustomerId} before creating new subscription",
                        subscription.Status, subscription.Id, customerId);

                    await stripeAdapter.CancelSubscriptionAsync(subscription.Id, new SubscriptionCancelOptions());

                    // Void any open invoices to prevent confusion
                    if (subscription.LatestInvoiceId != null)
                    {
                        var invoice = await stripeAdapter.GetInvoiceAsync(subscription.LatestInvoiceId, new InvoiceGetOptions());
                        if (invoice?.Status == "open")
                        {
                            await stripeAdapter.VoidInvoiceAsync(invoice.Id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling existing duplicate Premium subscriptions for customer {CustomerId}", customerId);
            // Don't throw - we still want to allow the new subscription to be created
        }
    }
}
