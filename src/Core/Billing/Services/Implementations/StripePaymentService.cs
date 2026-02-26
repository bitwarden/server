// FIXME: Update this file to be null safe and then delete the line below

#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.BitStripe;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Stripe;
using PaymentMethod = Stripe.PaymentMethod;
using StaticStore = Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Services.Implementations;

public class StripePaymentService : IStripePaymentService
{
    private const string SecretsManagerStandaloneDiscountId = "sm-standalone";

    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly Braintree.IBraintreeGateway _btGateway;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IGlobalSettings _globalSettings;
    private readonly IPricingClient _pricingClient;

    public StripePaymentService(
        ITransactionRepository transactionRepository,
        ILogger<StripePaymentService> logger,
        IStripeAdapter stripeAdapter,
        Braintree.IBraintreeGateway braintreeGateway,
        IGlobalSettings globalSettings,
        IPricingClient pricingClient)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
        _stripeAdapter = stripeAdapter;
        _btGateway = braintreeGateway;
        _globalSettings = globalSettings;
        _pricingClient = pricingClient;
    }

    private async Task ChangeOrganizationSponsorship(
        Organization org,
        OrganizationSponsorship sponsorship,
        bool applySponsorship)
    {
        var existingPlan = await _pricingClient.GetPlanOrThrow(org.PlanType);
        var sponsoredPlan = sponsorship?.PlanSponsorshipType != null
            ? SponsoredPlans.Get(sponsorship.PlanSponsorshipType.Value)
            : null;
        var subscriptionUpdate =
            new SponsorOrganizationSubscriptionUpdate(existingPlan, sponsoredPlan, applySponsorship);

        await FinalizeSubscriptionChangeAsync(org, subscriptionUpdate, true);

        var sub = await _stripeAdapter.GetSubscriptionAsync(org.GatewaySubscriptionId);
        org.ExpirationDate = sub.GetCurrentPeriodEnd();

        if (sponsorship is not null)
        {
            sponsorship.ValidUntil = sub.GetCurrentPeriodEnd();
        }
    }

    public Task SponsorOrganizationAsync(Organization org, OrganizationSponsorship sponsorship) =>
        ChangeOrganizationSponsorship(org, sponsorship, true);

    public Task RemoveOrganizationSponsorshipAsync(Organization org, OrganizationSponsorship sponsorship) =>
        ChangeOrganizationSponsorship(org, sponsorship, false);

    private async Task<string> FinalizeSubscriptionChangeAsync(ISubscriber subscriber,
        SubscriptionUpdate subscriptionUpdate, bool invoiceNow = false)
    {
        // remember, when in doubt, throw
        var subGetOptions = new SubscriptionGetOptions { Expand = ["customer.tax", "customer.tax_ids"] };
        var sub = await _stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId, subGetOptions);
        if (sub == null)
        {
            throw new GatewayException("Subscription not found.");
        }

        if (sub.Status == SubscriptionStatuses.Canceled)
        {
            throw new BadRequestException(
                "You do not have an active subscription. Reinstate your subscription to make changes.");
        }

        var existingCoupon = sub.Customer.Discount?.Coupon?.Id;

        var collectionMethod = sub.CollectionMethod;
        var daysUntilDue = sub.DaysUntilDue;
        var chargeNow = collectionMethod == "charge_automatically";
        var updatedItemOptions = subscriptionUpdate.UpgradeItemsOptions(sub);
        var isAnnualPlan = sub?.Items?.Data.FirstOrDefault()?.Plan?.Interval == "year";

        var subUpdateOptions = new SubscriptionUpdateOptions
        {
            Items = updatedItemOptions,
            ProrationBehavior = invoiceNow ? Core.Constants.AlwaysInvoice : Core.Constants.CreateProrations,
            DaysUntilDue = daysUntilDue ?? 1,
            CollectionMethod = "send_invoice"
        };
        if (!invoiceNow && isAnnualPlan && sub.Status.Trim() != "trialing")
        {
            subUpdateOptions.PendingInvoiceItemInterval =
                new SubscriptionPendingInvoiceItemIntervalOptions { Interval = "month" };
        }

        if (subscriptionUpdate is CompleteSubscriptionUpdate)
        {
            if (sub.Customer is
                {
                    Address.Country: not Core.Constants.CountryAbbreviations.UnitedStates,
                    TaxExempt: not StripeConstants.TaxExempt.Reverse
                })
            {
                await _stripeAdapter.UpdateCustomerAsync(sub.CustomerId,
                    new CustomerUpdateOptions { TaxExempt = StripeConstants.TaxExempt.Reverse });
            }

            subUpdateOptions.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        }

        if (!subscriptionUpdate.UpdateNeeded(sub))
        {
            // No need to update subscription, quantity matches
            return null;
        }

        string paymentIntentClientSecret = null;
        try
        {
            var subResponse = await _stripeAdapter.UpdateSubscriptionAsync(sub.Id, subUpdateOptions);

            var invoice = await _stripeAdapter.GetInvoiceAsync(subResponse?.LatestInvoiceId, new InvoiceGetOptions());
            if (invoice == null)
            {
                throw new BadRequestException("Unable to locate draft invoice for subscription update.");
            }

            if (invoice.AmountDue > 0 && updatedItemOptions.Any(i => i.Quantity > 0))
            {
                try
                {
                    if (invoiceNow)
                    {
                        if (chargeNow)
                        {
                            paymentIntentClientSecret =
                                await PayInvoiceAfterSubscriptionChangeAsync(subscriber, invoice);
                        }
                        else
                        {
                            invoice = await _stripeAdapter.FinalizeInvoiceAsync(subResponse.LatestInvoiceId,
                                new InvoiceFinalizeOptions { AutoAdvance = false, });
                            await _stripeAdapter.SendInvoiceAsync(invoice.Id, new InvoiceSendOptions());
                            paymentIntentClientSecret = null;
                        }
                    }
                }
                catch
                {
                    // Need to revert the subscription
                    await _stripeAdapter.UpdateSubscriptionAsync(sub.Id, new SubscriptionUpdateOptions
                    {
                        Items = subscriptionUpdate.RevertItemsOptions(sub),
                        // This proration behavior prevents a false "credit" from
                        //  being applied forward to the next month's invoice
                        ProrationBehavior = "none",
                        CollectionMethod = collectionMethod,
                        DaysUntilDue = daysUntilDue,
                    });
                    throw;
                }
            }
            else if (invoice.Status != StripeConstants.InvoiceStatus.Paid)
            {
                // Pay invoice with no charge to the customer this completes the invoice immediately without waiting the scheduled 1h
                invoice = await _stripeAdapter.PayInvoiceAsync(subResponse.LatestInvoiceId);
                paymentIntentClientSecret = null;
            }
        }
        finally
        {
            // Change back the subscription collection method and/or days until due
            if (collectionMethod != "send_invoice" || daysUntilDue == null)
            {
                await _stripeAdapter.UpdateSubscriptionAsync(sub.Id,
                    new SubscriptionUpdateOptions
                    {
                        CollectionMethod = collectionMethod,
                        DaysUntilDue = daysUntilDue,
                    });
            }

            var customer = await _stripeAdapter.GetCustomerAsync(sub.CustomerId);

            var newCoupon = customer.Discount?.Coupon?.Id;

            if (!string.IsNullOrEmpty(existingCoupon) && string.IsNullOrEmpty(newCoupon))
            {
                // Re-add the lost coupon due to the update.
                await _stripeAdapter.UpdateSubscriptionAsync(sub.Id, new SubscriptionUpdateOptions
                {
                    Discounts =
                    [
                        new SubscriptionDiscountOptions
                        {
                            Coupon = existingCoupon
                        }
                    ]
                });
            }
        }

        return paymentIntentClientSecret;
    }

    public async Task<string> AdjustSubscription(
        Organization organization,
        StaticStore.Plan updatedPlan,
        int newlyPurchasedPasswordManagerSeats,
        bool subscribedToSecretsManager,
        int? newlyPurchasedSecretsManagerSeats,
        int? newlyPurchasedAdditionalSecretsManagerServiceAccounts,
        int newlyPurchasedAdditionalStorage)
    {
        var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);
        return await FinalizeSubscriptionChangeAsync(
            organization,
            new CompleteSubscriptionUpdate(
                organization,
                plan,
                new SubscriptionData
                {
                    Plan = updatedPlan,
                    PurchasedPasswordManagerSeats = newlyPurchasedPasswordManagerSeats,
                    SubscribedToSecretsManager = subscribedToSecretsManager,
                    PurchasedSecretsManagerSeats = newlyPurchasedSecretsManagerSeats,
                    PurchasedAdditionalSecretsManagerServiceAccounts =
                        newlyPurchasedAdditionalSecretsManagerServiceAccounts,
                    PurchasedAdditionalStorage = newlyPurchasedAdditionalStorage
                }), true);
    }

    public Task<string> AdjustSeatsAsync(Organization organization, StaticStore.Plan plan, int additionalSeats) =>
        FinalizeSubscriptionChangeAsync(organization, new SeatSubscriptionUpdate(organization, plan, additionalSeats));

    public Task<string> AdjustSmSeatsAsync(Organization organization, StaticStore.Plan plan, int additionalSeats) =>
        FinalizeSubscriptionChangeAsync(
            organization,
            new SmSeatSubscriptionUpdate(organization, plan, additionalSeats));

    public Task<string> AdjustServiceAccountsAsync(
        Organization organization,
        StaticStore.Plan plan,
        int additionalServiceAccounts) =>
        FinalizeSubscriptionChangeAsync(
            organization,
            new ServiceAccountSubscriptionUpdate(organization, plan, additionalServiceAccounts));

    public Task<string> AdjustStorageAsync(
        IStorableSubscriber storableSubscriber,
        int additionalStorage,
        string storagePlanId)
    {
        return FinalizeSubscriptionChangeAsync(
            storableSubscriber,
            new StorageSubscriptionUpdate(storagePlanId, additionalStorage));
    }

    public async Task CancelAndRecoverChargesAsync(ISubscriber subscriber)
    {
        if (!string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
        {
            await _stripeAdapter.CancelSubscriptionAsync(subscriber.GatewaySubscriptionId,
                new SubscriptionCancelOptions());
        }

        if (string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
        {
            return;
        }

        var customer = await _stripeAdapter.GetCustomerAsync(subscriber.GatewayCustomerId);
        if (customer == null)
        {
            return;
        }

        if (customer.Metadata.ContainsKey("btCustomerId"))
        {
            var transactionRequest = new Braintree.TransactionSearchRequest()
                .CustomerId.Is(customer.Metadata["btCustomerId"]);
            var transactions = _btGateway.Transaction.Search(transactionRequest);

            if ((transactions?.MaximumCount ?? 0) > 0)
            {
                var txs = transactions.Cast<Braintree.Transaction>().Where(c => c.RefundedTransactionId == null);
                foreach (var transaction in txs)
                {
                    await _btGateway.Transaction.RefundAsync(transaction.Id);
                }
            }

            await _btGateway.Customer.DeleteAsync(customer.Metadata["btCustomerId"]);
        }
        else
        {
            var charges = await _stripeAdapter.ListChargesAsync(new ChargeListOptions
            {
                Customer = subscriber.GatewayCustomerId
            });

            if (charges?.Data != null)
            {
                foreach (var charge in charges.Data.Where(c => c.Captured && !c.Refunded))
                {
                    await _stripeAdapter.CreateRefundAsync(new RefundCreateOptions { Charge = charge.Id });
                }
            }
        }

        await _stripeAdapter.DeleteCustomerAsync(subscriber.GatewayCustomerId);
    }

    public async Task<string> PayInvoiceAfterSubscriptionChangeAsync(ISubscriber subscriber, Invoice invoice)
    {
        var customerOptions = new CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        var customer = await _stripeAdapter.GetCustomerAsync(subscriber.GatewayCustomerId, customerOptions);

        string paymentIntentClientSecret = null;

        // Invoice them and pay now instead of waiting until Stripe does this automatically.

        string cardPaymentMethodId = null;
        if (!customer.Metadata.ContainsKey("btCustomerId"))
        {
            var hasDefaultCardPaymentMethod = customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card";
            var hasDefaultValidSource = customer.DefaultSource != null &&
                                        (customer.DefaultSource is Card || customer.DefaultSource is BankAccount);
            if (!hasDefaultCardPaymentMethod && !hasDefaultValidSource)
            {
                cardPaymentMethodId = GetLatestCardPaymentMethod(customer.Id)?.Id;
                if (cardPaymentMethodId == null)
                {
                    // We're going to delete this draft invoice, it can't be paid
                    try
                    {
                        await _stripeAdapter.DeleteInvoiceAsync(invoice.Id);
                    }
                    catch
                    {
                        await _stripeAdapter.FinalizeInvoiceAsync(invoice.Id,
                            new InvoiceFinalizeOptions { AutoAdvance = false });
                        await _stripeAdapter.VoidInvoiceAsync(invoice.Id);
                    }

                    throw new BadRequestException("No payment method is available.");
                }
            }
        }

        Braintree.Transaction braintreeTransaction = null;
        try
        {
            // Finalize the invoice (from Draft) w/o auto-advance so we
            //  can attempt payment manually.
            invoice = await _stripeAdapter.FinalizeInvoiceAsync(invoice.Id,
                new InvoiceFinalizeOptions { AutoAdvance = false, });
            var invoicePayOptions = new InvoicePayOptions { PaymentMethod = cardPaymentMethodId, };
            if (customer?.Metadata?.ContainsKey("btCustomerId") ?? false)
            {
                invoicePayOptions.PaidOutOfBand = true;
                var btInvoiceAmount = (invoice.AmountDue / 100M);
                var transactionResult = await _btGateway.Transaction.SaleAsync(
                    new Braintree.TransactionRequest
                    {
                        Amount = btInvoiceAmount,
                        CustomerId = customer.Metadata["btCustomerId"],
                        Options = new Braintree.TransactionOptionsRequest
                        {
                            SubmitForSettlement = true,
                            PayPal = new Braintree.TransactionOptionsPayPalRequest
                            {
                                CustomField =
                                    $"{subscriber.BraintreeIdField()}:{subscriber.Id},{subscriber.BraintreeCloudRegionField()}:{_globalSettings.BaseServiceUri.CloudRegion}"
                            }
                        },
                        CustomFields = new Dictionary<string, string>
                        {
                            [subscriber.BraintreeIdField()] = subscriber.Id.ToString(),
                            [subscriber.BraintreeCloudRegionField()] =
                                _globalSettings.BaseServiceUri.CloudRegion
                        }
                    });

                if (!transactionResult.IsSuccess())
                {
                    throw new GatewayException("Failed to charge PayPal customer.");
                }

                braintreeTransaction = transactionResult.Target;
                invoice = await _stripeAdapter.UpdateInvoiceAsync(invoice.Id, new InvoiceUpdateOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["btTransactionId"] = braintreeTransaction.Id,
                        ["btPayPalTransactionId"] =
                            braintreeTransaction.PayPalDetails.AuthorizationId
                    },
                });
                invoicePayOptions.PaidOutOfBand = true;
            }

            try
            {
                invoice = await _stripeAdapter.PayInvoiceAsync(invoice.Id, invoicePayOptions);
            }
            catch (StripeException e)
            {
                if (e.HttpStatusCode == System.Net.HttpStatusCode.PaymentRequired &&
                    e.StripeError?.Code == "invoice_payment_intent_requires_action")
                {
                    // SCA required, get intent client secret
                    var invoiceGetOptions = new InvoiceGetOptions();
                    invoiceGetOptions.AddExpand("confirmation_secret");
                    invoice = await _stripeAdapter.GetInvoiceAsync(invoice.Id, invoiceGetOptions);
                    paymentIntentClientSecret = invoice?.ConfirmationSecret?.ClientSecret;
                }
                else
                {
                    throw new GatewayException("Unable to pay invoice.");
                }
            }
        }
        catch (Exception e)
        {
            if (braintreeTransaction != null)
            {
                await _btGateway.Transaction.RefundAsync(braintreeTransaction.Id);
            }

            if (invoice != null)
            {
                if (invoice.Status == "paid")
                {
                    // It's apparently paid, so we need to return w/o throwing an exception
                    return paymentIntentClientSecret;
                }

                invoice = await _stripeAdapter.VoidInvoiceAsync(invoice.Id, new InvoiceVoidOptions());

                // HACK: Workaround for customer balance credit
                if (invoice.StartingBalance < 0)
                {
                    // Customer had a balance applied to this invoice. Since we can't fully trust Stripe to
                    //  credit it back to the customer (even though their docs claim they will), we need to
                    //  check that balance against the current customer balance and determine if it needs to be re-applied
                    customer = await _stripeAdapter.GetCustomerAsync(subscriber.GatewayCustomerId, customerOptions);

                    // Assumption: Customer balance should now be $0, otherwise payment would not have failed.
                    if (customer.Balance == 0)
                    {
                        await _stripeAdapter.UpdateCustomerAsync(customer.Id,
                            new CustomerUpdateOptions { Balance = invoice.StartingBalance });
                    }
                }
            }

            if (e is StripeException strEx &&
                (strEx.StripeError?.Message?.Contains("cannot be used because it is not verified") ?? false))
            {
                throw new GatewayException("Bank account is not yet verified.");
            }

            // Let the caller perform any subscription change cleanup
            throw;
        }

        return paymentIntentClientSecret;
    }

    public async Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false)
    {
        if (subscriber == null)
        {
            throw new ArgumentNullException(nameof(subscriber));
        }

        if (string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
        {
            throw new GatewayException("No subscription.");
        }

        var sub = await _stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId);
        if (sub == null)
        {
            throw new GatewayException("Subscription was not found.");
        }

        if (sub.CanceledAt.HasValue || sub.Status == "canceled" || sub.Status == "unpaid" ||
            sub.Status == "incomplete_expired")
        {
            // Already canceled
            return;
        }

        try
        {
            var canceledSub = endOfPeriod
                ? await _stripeAdapter.UpdateSubscriptionAsync(sub.Id,
                    new SubscriptionUpdateOptions { CancelAtPeriodEnd = true })
                : await _stripeAdapter.CancelSubscriptionAsync(sub.Id, new SubscriptionCancelOptions());
            if (!canceledSub.CanceledAt.HasValue)
            {
                throw new GatewayException("Unable to cancel subscription.");
            }
        }
        catch (StripeException e)
        {
            if (e.Message != $"No such subscription: {subscriber.GatewaySubscriptionId}")
            {
                throw;
            }
        }
    }

    public async Task ReinstateSubscriptionAsync(ISubscriber subscriber)
    {
        if (subscriber == null)
        {
            throw new ArgumentNullException(nameof(subscriber));
        }

        if (string.IsNullOrWhiteSpace(subscriber.GatewaySubscriptionId))
        {
            throw new GatewayException("No subscription.");
        }

        var sub = await _stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId);
        if (sub == null)
        {
            throw new GatewayException("Subscription was not found.");
        }

        if ((sub.Status != "active" && sub.Status != "trialing" && !sub.Status.StartsWith("incomplete")) ||
            !sub.CanceledAt.HasValue)
        {
            throw new GatewayException("Subscription is not marked for cancellation.");
        }

        var updatedSub = await _stripeAdapter.UpdateSubscriptionAsync(sub.Id,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = false });
        if (updatedSub.CanceledAt.HasValue)
        {
            throw new GatewayException("Unable to reinstate subscription.");
        }
    }

    public async Task<bool> CreditAccountAsync(ISubscriber subscriber, decimal creditAmount)
    {
        Customer customer = null;
        var customerExists = subscriber.Gateway == GatewayType.Stripe &&
                             !string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId);
        if (customerExists)
        {
            customer = await _stripeAdapter.GetCustomerAsync(subscriber.GatewayCustomerId);
        }
        else
        {
            customer = await _stripeAdapter.CreateCustomerAsync(new CustomerCreateOptions
            {
                Email = subscriber.BillingEmailAddress(),
                Description = subscriber.BillingName(),
            });
            subscriber.Gateway = GatewayType.Stripe;
            subscriber.GatewayCustomerId = customer.Id;
        }

        await _stripeAdapter.UpdateCustomerAsync(customer.Id,
            new CustomerUpdateOptions { Balance = customer.Balance - (long)(creditAmount * 100) });
        return !customerExists;
    }

    public async Task<BillingInfo> GetBillingAsync(ISubscriber subscriber)
    {
        var customer = await GetCustomerAsync(subscriber.GatewayCustomerId, GetCustomerPaymentOptions());
        var billingInfo = new BillingInfo
        {
            Balance = customer.GetBillingBalance(),
            PaymentSource = await GetBillingPaymentSourceAsync(customer)
        };

        return billingInfo;
    }

    public async Task<BillingHistoryInfo> GetBillingHistoryAsync(ISubscriber subscriber)
    {
        var customer = await GetCustomerAsync(subscriber.GatewayCustomerId);
        var billingInfo = new BillingHistoryInfo
        {
            Transactions = await GetBillingTransactionsAsync(subscriber, 20),
            Invoices = await GetBillingInvoicesAsync(customer, 20)
        };

        return billingInfo;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber)
    {
        var subscriptionInfo = new SubscriptionInfo();

        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            return subscriptionInfo;
        }

        var subscription = await _stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId,
            new SubscriptionGetOptions { Expand = ["customer.discount.coupon.applies_to", "discounts.coupon.applies_to", "test_clock"] });

        if (subscription == null)
        {
            return subscriptionInfo;
        }

        subscriptionInfo.Subscription = new SubscriptionInfo.BillingSubscription(subscription);

        // Discount selection priority:
        // 1. Customer-level discount (applies to all subscriptions for the customer)
        // 2. First subscription-level discount (if multiple exist, FirstOrDefault() selects the first one)
        // Note: When multiple subscription-level discounts exist, only the first one is used.
        // This matches Stripe's behavior where the first discount in the list is applied.
        // Defensive null checks: Even though we expand "customer" and "discounts", external APIs
        // may not always return the expected data structure, so we use null-safe operators.
        var discount = subscription.Customer?.Discount ?? subscription.Discounts?.FirstOrDefault();

        if (discount != null)
        {
            subscriptionInfo.CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount(discount);
        }

        var (suspensionDate, unpaidPeriodEndDate) = await GetSuspensionDateAsync(subscription);

        if (suspensionDate.HasValue && unpaidPeriodEndDate.HasValue)
        {
            subscriptionInfo.Subscription.SuspensionDate = suspensionDate;
            subscriptionInfo.Subscription.UnpaidPeriodEndDate = unpaidPeriodEndDate;
        }

        if (subscription is { CanceledAt: not null } || string.IsNullOrWhiteSpace(subscriber.GatewayCustomerId))
        {
            return subscriptionInfo;
        }

        try
        {
            var invoiceCreatePreviewOptions = new InvoiceCreatePreviewOptions
            {
                Customer = subscriber.GatewayCustomerId,
                Subscription = subscriber.GatewaySubscriptionId
            };

            var upcomingInvoice = await _stripeAdapter.CreateInvoicePreviewAsync(invoiceCreatePreviewOptions);

            if (upcomingInvoice != null)
            {
                subscriptionInfo.UpcomingInvoice = new SubscriptionInfo.BillingUpcomingInvoice(upcomingInvoice);
            }
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to retrieve upcoming invoice for customer {CustomerId}, subscription {SubscriptionId}. Error Code: {ErrorCode}",
                subscriber.GatewayCustomerId,
                subscriber.GatewaySubscriptionId,
                ex.StripeError?.Code);
        }

        return subscriptionInfo;
    }

    public async Task<string> AddSecretsManagerToSubscription(
        Organization org,
        StaticStore.Plan plan,
        int additionalSmSeats,
        int additionalServiceAccount) =>
        await FinalizeSubscriptionChangeAsync(
            org,
            new SecretsManagerSubscribeUpdate(org, plan, additionalSmSeats, additionalServiceAccount),
            true);

    public async Task<bool> HasSecretsManagerStandalone(Organization organization) =>
        await HasSecretsManagerStandaloneAsync(gatewayCustomerId: organization.GatewayCustomerId,
            organizationHasSecretsManager: organization.UseSecretsManager);

    public async Task<bool> HasSecretsManagerStandalone(InviteOrganization organization) =>
        await HasSecretsManagerStandaloneAsync(gatewayCustomerId: organization.GatewayCustomerId,
            organizationHasSecretsManager: organization.UseSecretsManager);

    private async Task<bool> HasSecretsManagerStandaloneAsync(string gatewayCustomerId,
        bool organizationHasSecretsManager)
    {
        if (string.IsNullOrEmpty(gatewayCustomerId))
        {
            return false;
        }

        if (organizationHasSecretsManager is false)
        {
            return false;
        }

        var customer = await _stripeAdapter.GetCustomerAsync(gatewayCustomerId);

        return customer?.Discount?.Coupon?.Id == SecretsManagerStandaloneDiscountId;
    }

    private async Task<(DateTime?, DateTime?)> GetSuspensionDateAsync(Subscription subscription)
    {
        if (subscription.Status is not "past_due" && subscription.Status is not "unpaid")
        {
            return (null, null);
        }

        var openInvoices = await _stripeAdapter.SearchInvoiceAsync(new InvoiceSearchOptions
        {
            Query = $"subscription:'{subscription.Id}' status:'open'"
        });

        if (openInvoices.Count == 0)
        {
            return (null, null);
        }

        var currentDate = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        switch (subscription.CollectionMethod)
        {
            case "charge_automatically":
                {
                    var firstOverdueInvoice = openInvoices
                        .Where(invoice => invoice.PeriodEnd < currentDate && invoice.Attempted)
                        .MinBy(invoice => invoice.Created);

                    return (firstOverdueInvoice?.Created.AddDays(14), firstOverdueInvoice?.PeriodEnd);
                }
            case "send_invoice":
                {
                    var firstOverdueInvoice = openInvoices
                        .Where(invoice => invoice.DueDate < currentDate)
                        .MinBy(invoice => invoice.Created);

                    return (firstOverdueInvoice?.DueDate?.AddDays(30), firstOverdueInvoice?.PeriodEnd);
                }
            default: return (null, null);
        }
    }

    private PaymentMethod GetLatestCardPaymentMethod(string customerId)
    {
        var cardPaymentMethods = _stripeAdapter.ListPaymentMethodsAutoPaging(
            new PaymentMethodListOptions { Customer = customerId, Type = "card" });
        return cardPaymentMethods.OrderByDescending(m => m.Created).FirstOrDefault();
    }

    private async Task<BillingInfo.BillingSource> GetBillingPaymentSourceAsync(Customer customer)
    {
        if (customer == null)
        {
            return null;
        }

        if (customer.Metadata?.ContainsKey("btCustomerId") ?? false)
        {
            try
            {
                var braintreeCustomer = await _btGateway.Customer.FindAsync(
                    customer.Metadata["btCustomerId"]);
                if (braintreeCustomer?.DefaultPaymentMethod != null)
                {
                    return new BillingInfo.BillingSource(
                        braintreeCustomer.DefaultPaymentMethod);
                }
            }
            catch (Braintree.Exceptions.NotFoundException)
            {
            }
        }

        if (customer.InvoiceSettings?.DefaultPaymentMethod?.Type == "card")
        {
            return new BillingInfo.BillingSource(
                customer.InvoiceSettings.DefaultPaymentMethod);
        }

        if (customer.DefaultSource != null &&
            (customer.DefaultSource is Card || customer.DefaultSource is BankAccount))
        {
            return new BillingInfo.BillingSource(customer.DefaultSource);
        }

        var paymentMethod = GetLatestCardPaymentMethod(customer.Id);
        return paymentMethod != null ? new BillingInfo.BillingSource(paymentMethod) : null;
    }

    private CustomerGetOptions GetCustomerPaymentOptions()
    {
        var customerOptions = new CustomerGetOptions();
        customerOptions.AddExpand("default_source");
        customerOptions.AddExpand("invoice_settings.default_payment_method");
        return customerOptions;
    }

    private async Task<Customer> GetCustomerAsync(string gatewayCustomerId, CustomerGetOptions options = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayCustomerId))
        {
            return null;
        }

        Customer customer = null;
        try
        {
            customer = await _stripeAdapter.GetCustomerAsync(gatewayCustomerId, options);
        }
        catch (StripeException)
        {
        }

        return customer;
    }

    private async Task<IEnumerable<BillingHistoryInfo.BillingTransaction>> GetBillingTransactionsAsync(
        ISubscriber subscriber, int? limit = null)
    {
        var transactions = subscriber switch
        {
            User => await _transactionRepository.GetManyByUserIdAsync(subscriber.Id, limit),
            Organization => await _transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id, limit),
            _ => null
        };

        return transactions?.OrderByDescending(i => i.CreationDate)
            .Select(t => new BillingHistoryInfo.BillingTransaction(t));
    }

    private async Task<IEnumerable<BillingHistoryInfo.BillingInvoice>> GetBillingInvoicesAsync(Customer customer,
        int? limit = null)
    {
        if (customer == null)
        {
            return null;
        }

        try
        {
            var paidInvoicesTask = _stripeAdapter.ListInvoicesAsync(new StripeInvoiceListOptions
            {
                Customer = customer.Id,
                SelectAll = !limit.HasValue,
                Limit = limit,
                Status = "paid"
            });
            var openInvoicesTask = _stripeAdapter.ListInvoicesAsync(new StripeInvoiceListOptions
            {
                Customer = customer.Id,
                SelectAll = !limit.HasValue,
                Limit = limit,
                Status = "open"
            });
            var uncollectibleInvoicesTask = _stripeAdapter.ListInvoicesAsync(new StripeInvoiceListOptions
            {
                Customer = customer.Id,
                SelectAll = !limit.HasValue,
                Limit = limit,
                Status = "uncollectible"
            });

            var paidInvoices = await paidInvoicesTask;
            var openInvoices = await openInvoicesTask;
            var uncollectibleInvoices = await uncollectibleInvoicesTask;

            var invoices = paidInvoices
                .Concat(openInvoices)
                .Concat(uncollectibleInvoices);

            var result = invoices
                .OrderByDescending(invoice => invoice.Created)
                .Select(invoice => new BillingHistoryInfo.BillingInvoice(invoice));

            return limit.HasValue
                ? result.Take(limit.Value)
                : result;
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "An error occurred while listing Stripe invoices");
            throw new GatewayException("Failed to retrieve current invoices", exception);
        }
    }
}
