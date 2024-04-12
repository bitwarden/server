using Bit.Billing.Constants;
using Bit.Billing.Models;
using Bit.Billing.Services;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Braintree;
using Braintree.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Stripe;
using Customer = Stripe.Customer;
using Event = Stripe.Event;
using JsonSerializer = System.Text.Json.JsonSerializer;
using PaymentMethod = Stripe.PaymentMethod;
using Subscription = Stripe.Subscription;
using TaxRate = Bit.Core.Entities.TaxRate;
using Transaction = Bit.Core.Entities.Transaction;
using TransactionType = Bit.Core.Enums.TransactionType;

namespace Bit.Billing.Controllers;

[Route("stripe")]
public class StripeController : Controller
{
    private const string PremiumPlanId = "premium-annually";
    private const string PremiumPlanIdAppStore = "premium-annually-app";

    private readonly BillingSettings _billingSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly IOrganizationService _organizationService;
    private readonly IValidateSponsorshipCommand _validateSponsorshipCommand;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserService _userService;
    private readonly IMailService _mailService;
    private readonly ILogger<StripeController> _logger;
    private readonly BraintreeGateway _btGateway;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IFeatureService _featureService;

    public StripeController(
        GlobalSettings globalSettings,
        IOptions<BillingSettings> billingSettings,
        IWebHostEnvironment hostingEnvironment,
        IOrganizationService organizationService,
        IValidateSponsorshipCommand validateSponsorshipCommand,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IOrganizationRepository organizationRepository,
        ITransactionRepository transactionRepository,
        IUserService userService,
        IMailService mailService,
        IReferenceEventService referenceEventService,
        ILogger<StripeController> logger,
        ITaxRateRepository taxRateRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IFeatureService featureService)
    {
        _billingSettings = billingSettings?.Value;
        _hostingEnvironment = hostingEnvironment;
        _organizationService = organizationService;
        _validateSponsorshipCommand = validateSponsorshipCommand;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _organizationRepository = organizationRepository;
        _transactionRepository = transactionRepository;
        _userService = userService;
        _mailService = mailService;
        _referenceEventService = referenceEventService;
        _taxRateRepository = taxRateRepository;
        _userRepository = userRepository;
        _logger = logger;
        _btGateway = new BraintreeGateway
        {
            Environment = globalSettings.Braintree.Production ?
                Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
            MerchantId = globalSettings.Braintree.MerchantId,
            PublicKey = globalSettings.Braintree.PublicKey,
            PrivateKey = globalSettings.Braintree.PrivateKey
        };
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _featureService = featureService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> PostWebhook([FromQuery] string key)
    {
        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.StripeWebhookKey))
        {
            return new BadRequestResult();
        }

        Event parsedEvent;
        using (var sr = new StreamReader(HttpContext.Request.Body))
        {
            var json = await sr.ReadToEndAsync();
            var webhookSecret = PickStripeWebhookSecret(json);

            if (string.IsNullOrEmpty(webhookSecret))
            {
                return new OkResult();
            }

            parsedEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"],
                webhookSecret,
                throwOnApiVersionMismatch: false);
        }

        if (StripeConfiguration.ApiVersion != parsedEvent.ApiVersion)
        {
            _logger.LogWarning(
                "Stripe {WebhookType} webhook's API version ({WebhookAPIVersion}) does not match SDK API Version ({SDKAPIVersion})",
                parsedEvent.Type,
                parsedEvent.ApiVersion,
                StripeConfiguration.ApiVersion);

            return new OkResult();
        }

        if (string.IsNullOrWhiteSpace(parsedEvent?.Id))
        {
            _logger.LogWarning("No event id.");
            return new BadRequestResult();
        }

        if (_hostingEnvironment.IsProduction() && !parsedEvent.Livemode)
        {
            _logger.LogWarning("Getting test events in production.");
            return new BadRequestResult();
        }

        // If the customer and server cloud regions don't match, early return 200 to avoid unnecessary errors
        if (!await _stripeEventService.ValidateCloudRegion(parsedEvent))
        {
            return new OkResult();
        }

        var subDeleted = parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionDeleted);
        var subUpdated = parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionUpdated);

        if (subDeleted || subUpdated)
        {
            var subscription = await _stripeEventService.GetSubscription(parsedEvent, true);
            var ids = GetIdsFromMetaData(subscription.Metadata);
            var organizationId = ids.Item1 ?? Guid.Empty;
            var userId = ids.Item2 ?? Guid.Empty;
            var subCanceled = subDeleted && subscription.Status == StripeSubscriptionStatus.Canceled;
            var subUnpaid = subUpdated && subscription.Status == StripeSubscriptionStatus.Unpaid;
            var subActive = subUpdated && subscription.Status == StripeSubscriptionStatus.Active;
            var subIncompleteExpired = subUpdated && subscription.Status == StripeSubscriptionStatus.IncompleteExpired;

            if (subCanceled || subUnpaid || subIncompleteExpired)
            {
                // org
                if (organizationId != Guid.Empty)
                {
                    await _organizationService.DisableAsync(organizationId, subscription.CurrentPeriodEnd);
                }
                // user
                else if (userId != Guid.Empty)
                {
                    if (subUnpaid && subscription.Items.Any(i => i.Price.Id is PremiumPlanId or PremiumPlanIdAppStore))
                    {
                        await CancelSubscription(subscription.Id);
                        await VoidOpenInvoices(subscription.Id);
                    }

                    var user = await _userService.GetUserByIdAsync(userId);
                    if (user?.Premium == true)
                    {
                        await _userService.DisablePremiumAsync(userId, subscription.CurrentPeriodEnd);
                    }
                }
            }

            if (subActive)
            {

                if (organizationId != Guid.Empty)
                {
                    await _organizationService.EnableAsync(organizationId);
                }
                else if (userId != Guid.Empty)
                {
                    await _userService.EnablePremiumAsync(userId,
                        subscription.CurrentPeriodEnd);
                }
            }

            if (subUpdated)
            {
                // org
                if (organizationId != Guid.Empty)
                {
                    await _organizationService.UpdateExpirationDateAsync(organizationId,
                        subscription.CurrentPeriodEnd);
                    if (IsSponsoredSubscription(subscription))
                    {
                        await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(organizationId, subscription.CurrentPeriodEnd);
                    }
                }
                // user
                else if (userId != Guid.Empty)
                {
                    await _userService.UpdatePremiumExpirationAsync(userId,
                        subscription.CurrentPeriodEnd);
                }
            }
        }
        else switch (parsedEvent.Type)
            {
                case HandledStripeWebhook.UpcomingInvoice:
                    {
                        var invoice = await _stripeEventService.GetInvoice(parsedEvent);
                        await HandleUpcomingInvoiceEventAsync(invoice, parsedEvent.Id);
                        return Ok();
                    }
                case HandledStripeWebhook.ChargeSucceeded:
                    {
                        var charge = await _stripeEventService.GetCharge(parsedEvent);
                        await HandleChargeSucceededEventAsync(charge);
                        return Ok();
                    }
                case HandledStripeWebhook.ChargeRefunded:
                    {
                        var charge = await _stripeEventService.GetCharge(parsedEvent, true, ["refunds"]);
                        await HandleChargeRefundedEventAsync(charge);
                        return Ok();
                    }
                case HandledStripeWebhook.PaymentSucceeded:
                    {
                        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
                        await HandlePaymentSucceededEventAsync(invoice);
                        return Ok();
                    }
                case HandledStripeWebhook.PaymentFailed:
                    {
                        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
                        await HandlePaymentFailedEventAsync(invoice);
                        return Ok();
                    }
                case HandledStripeWebhook.InvoiceCreated:
                    {
                        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
                        await HandleInvoiceCreatedEventAsync(invoice);
                        return Ok();
                    }
                case HandledStripeWebhook.PaymentMethodAttached:
                    {
                        var paymentMethod = await _stripeEventService.GetPaymentMethod(parsedEvent);
                        await HandlePaymentMethodAttachedAsync(paymentMethod);
                        break;
                    }
                case HandledStripeWebhook.CustomerUpdated:
                    {
                        var customer =
                            await _stripeEventService.GetCustomer(parsedEvent, true, ["subscriptions"]);

                        if (customer.Subscriptions == null || !customer.Subscriptions.Any())
                        {
                            return new OkResult();
                        }

                        var subscription = customer.Subscriptions.First();

                        var (organizationId, _) = GetIdsFromMetaData(subscription.Metadata);

                        if (!organizationId.HasValue)
                        {
                            return new OkResult();
                        }

                        var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);
                        organization.BillingEmail = customer.Email;
                        await _organizationRepository.ReplaceAsync(organization);

                        await _referenceEventService.RaiseEventAsync(
                            new ReferenceEvent(ReferenceEventType.OrganizationEditedInStripe, organization, _currentContext));
                        break;
                    }
                default:
                    _logger.LogWarning("Unsupported event received. " + parsedEvent.Type);
                    break;
            }

        return new OkResult();
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.InvoiceCreated"/> event type from Stripe.
    /// </summary>
    /// <param name="invoice"></param>
    private async Task HandleInvoiceCreatedEventAsync(Invoice invoice)
    {
        if (invoice.Paid || !ShouldAttemptToPayInvoice(invoice))
        {
            return;
        }

        await AttemptToPayInvoiceAsync(invoice);
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.PaymentSucceeded"/> event type from Stripe.
    /// </summary>
    /// <param name="invoice"></param>
    private async Task HandlePaymentSucceededEventAsync(Invoice invoice)
    {
        if (invoice.Paid && invoice.BillingReason == "subscription_create")
        {
            var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);
            if (subscription?.Status == StripeSubscriptionStatus.Active)
            {
                if (DateTime.UtcNow - invoice.Created < TimeSpan.FromMinutes(1))
                {
                    await Task.Delay(5000);
                }

                var ids = GetIdsFromMetaData(subscription.Metadata);
                // org
                if (ids.Item1.HasValue)
                {
                    if (subscription.Items.Any(i => StaticStore.Plans.Any(p => p.PasswordManager.StripePlanId == i.Plan.Id)))
                    {
                        await _organizationService.EnableAsync(ids.Item1.Value, subscription.CurrentPeriodEnd);

                        var organization = await _organizationRepository.GetByIdAsync(ids.Item1.Value);
                        await _referenceEventService.RaiseEventAsync(
                            new ReferenceEvent(ReferenceEventType.Rebilled, organization, _currentContext)
                            {
                                PlanName = organization?.Plan,
                                PlanType = organization?.PlanType,
                                Seats = organization?.Seats,
                                Storage = organization?.MaxStorageGb,
                            });
                    }
                }
                // user
                else if (ids.Item2.HasValue)
                {
                    if (subscription.Items.Any(i => i.Plan.Id == PremiumPlanId))
                    {
                        await _userService.EnablePremiumAsync(ids.Item2.Value, subscription.CurrentPeriodEnd);

                        var user = await _userRepository.GetByIdAsync(ids.Item2.Value);
                        await _referenceEventService.RaiseEventAsync(
                            new ReferenceEvent(ReferenceEventType.Rebilled, user, _currentContext)
                            {
                                PlanName = PremiumPlanId,
                                Storage = user?.MaxStorageGb,
                            });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.ChargeRefunded"/> event type from Stripe.
    /// </summary>
    /// <param name="charge"></param>
    private async Task HandleChargeRefundedEventAsync(Charge charge)
    {
        var parentTransaction = await _transactionRepository.GetByGatewayIdAsync(GatewayType.Stripe, charge.Id);
        if (parentTransaction == null)
        {
            // Attempt to create a transaction for the charge if it doesn't exist
            var (organizationId, userId) = await GetEntityIdsFromChargeAsync(charge);
            var tx = FromChargeToTransaction(charge, organizationId, userId);
            try
            {
                parentTransaction = await _transactionRepository.CreateAsync(tx);
            }
            catch (SqlException e) when (e.Number == 547) // FK constraint violation
            {
                _logger.LogWarning(
                    "Charge refund could not create transaction as entity may have been deleted. {ChargeId}",
                    charge.Id);
                return;
            }
        }

        var amountRefunded = charge.AmountRefunded / 100M;

        if (parentTransaction.Refunded.GetValueOrDefault() ||
            parentTransaction.RefundedAmount.GetValueOrDefault() >= amountRefunded)
        {
            _logger.LogWarning(
                "Charge refund amount doesn't match parent transaction's amount or parent has already been refunded. {ChargeId}",
                charge.Id);
            return;
        }

        parentTransaction.RefundedAmount = amountRefunded;
        if (charge.Refunded)
        {
            parentTransaction.Refunded = true;
        }

        await _transactionRepository.ReplaceAsync(parentTransaction);

        foreach (var refund in charge.Refunds)
        {
            var refundTransaction = await _transactionRepository.GetByGatewayIdAsync(
                GatewayType.Stripe, refund.Id);
            if (refundTransaction != null)
            {
                continue;
            }

            await _transactionRepository.CreateAsync(new Transaction
            {
                Amount = refund.Amount / 100M,
                CreationDate = refund.Created,
                OrganizationId = parentTransaction.OrganizationId,
                UserId = parentTransaction.UserId,
                Type = TransactionType.Refund,
                Gateway = GatewayType.Stripe,
                GatewayId = refund.Id,
                PaymentMethodType = parentTransaction.PaymentMethodType,
                Details = parentTransaction.Details
            });
        }
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.ChargeSucceeded"/> event type from Stripe.
    /// </summary>
    /// <param name="charge"></param>
    private async Task HandleChargeSucceededEventAsync(Charge charge)
    {
        var existingTransaction = await _transactionRepository.GetByGatewayIdAsync(GatewayType.Stripe, charge.Id);
        if (existingTransaction is not null)
        {
            _logger.LogInformation("Charge success already processed. {ChargeId}", charge.Id);
            return;
        }

        var (organizationId, userId) = await GetEntityIdsFromChargeAsync(charge);
        if (!organizationId.HasValue && !userId.HasValue)
        {
            _logger.LogWarning("Charge success has no subscriber ids. {ChargeId}", charge.Id);
            return;
        }

        var transaction = FromChargeToTransaction(charge, organizationId, userId);
        if (!transaction.PaymentMethodType.HasValue)
        {
            _logger.LogWarning("Charge success from unsupported source/method. {ChargeId}", charge.Id);
            return;
        }

        try
        {
            await _transactionRepository.CreateAsync(transaction);
        }
        catch (SqlException e) when (e.Number == 547)
        {
            _logger.LogWarning(
                "Charge success could not create transaction as entity may have been deleted. {ChargeId}",
                charge.Id);
        }
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.UpcomingInvoice"/> event type from Stripe.
    /// </summary>
    /// <param name="invoice"></param>
    /// <param name="eventId"></param>
    /// <exception cref="Exception"></exception>
    private async Task HandleUpcomingInvoiceEventAsync(Invoice invoice, string eventId)
    {
        if (string.IsNullOrEmpty(invoice.SubscriptionId))
        {
            _logger.LogWarning("Received 'invoice.upcoming' Event with ID '{eventId}' that did not include a Subscription ID", eventId);
            return;
        }

        var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);

        if (subscription == null)
        {
            throw new Exception(
                $"Received null Subscription from Stripe for ID '{invoice.SubscriptionId}' while processing Event with ID '{eventId}'");
        }

        var pm5766AutomaticTaxIsEnabled = _featureService.IsEnabled(FeatureFlagKeys.PM5766AutomaticTax);
        if (pm5766AutomaticTaxIsEnabled)
        {
            var customerGetOptions = new CustomerGetOptions();
            customerGetOptions.AddExpand("tax");
            var customer = await _stripeFacade.GetCustomer(subscription.CustomerId, customerGetOptions);
            if (!subscription.AutomaticTax.Enabled &&
                customer.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported)
            {
                subscription = await _stripeFacade.UpdateSubscription(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        DefaultTaxRates = [],
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
        }

        var updatedSubscription = pm5766AutomaticTaxIsEnabled
            ? subscription
            : await VerifyCorrectTaxRateForCharge(invoice, subscription);

        var (organizationId, userId) = GetIdsFromMetaData(updatedSubscription.Metadata);

        var invoiceLineItemDescriptions = invoice.Lines.Select(i => i.Description).ToList();

        if (organizationId.HasValue)
        {
            if (IsSponsoredSubscription(updatedSubscription))
            {
                await _validateSponsorshipCommand.ValidateSponsorshipAsync(organizationId.Value);
            }

            var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);

            if (organization == null || !OrgPlanForInvoiceNotifications(organization))
            {
                return;
            }

            await SendEmails(new List<string> { organization.BillingEmail });

            /*
             * TODO: https://bitwarden.atlassian.net/browse/PM-4862
             * Disabling this as part of a hot fix. It needs to check whether the organization
             * belongs to a Reseller provider and only send an email to the organization owners if it does.
             * It also requires a new email template as the current one contains too much billing information.
             */

            // var ownerEmails = await _organizationRepository.GetOwnerEmailAddressesById(organization.Id);

            // await SendEmails(ownerEmails);
        }
        else if (userId.HasValue)
        {
            var user = await _userService.GetUserByIdAsync(userId.Value);

            if (user?.Premium == true)
            {
                await SendEmails(new List<string> { user.Email });
            }
        }

        return;

        /*
         * Sends emails to the given email addresses.
         */
        async Task SendEmails(IEnumerable<string> emails)
        {
            var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));

            if (invoice.NextPaymentAttempt.HasValue)
            {
                await _mailService.SendInvoiceUpcoming(
                    validEmails,
                    invoice.AmountDue / 100M,
                    invoice.NextPaymentAttempt.Value,
                    invoiceLineItemDescriptions,
                    true);
            }
        }
    }

    /// <summary>
    /// Gets the organization or user ID from the metadata of a Stripe Charge object.
    /// </summary>
    /// <param name="charge"></param>
    /// <returns></returns>
    private async Task<(Guid?, Guid?)> GetEntityIdsFromChargeAsync(Charge charge)
    {
        Guid? organizationId = null;
        Guid? userId = null;

        if (charge.InvoiceId != null)
        {
            var invoice = await _stripeFacade.GetInvoice(charge.InvoiceId);
            if (invoice?.SubscriptionId != null)
            {
                var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);
                (organizationId, userId) = GetIdsFromMetaData(subscription?.Metadata);
            }
        }

        if (organizationId.HasValue || userId.HasValue)
        {
            return (organizationId, userId);
        }

        var subscriptions = await _stripeFacade.ListSubscriptions(new SubscriptionListOptions
        {
            Customer = charge.CustomerId
        });

        foreach (var subscription in subscriptions)
        {
            if (subscription.Status is StripeSubscriptionStatus.Canceled or StripeSubscriptionStatus.IncompleteExpired)
            {
                continue;
            }

            (organizationId, userId) = GetIdsFromMetaData(subscription.Metadata);

            if (organizationId.HasValue || userId.HasValue)
            {
                return (organizationId, userId);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Converts a Stripe Charge object to a Bitwarden Transaction object.
    /// </summary>
    /// <param name="charge"></param>
    /// <param name="organizationId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    private static Transaction FromChargeToTransaction(Charge charge, Guid? organizationId, Guid? userId)
    {
        var transaction = new Transaction
        {
            Amount = charge.Amount / 100M,
            CreationDate = charge.Created,
            OrganizationId = organizationId,
            UserId = userId,
            Type = TransactionType.Charge,
            Gateway = GatewayType.Stripe,
            GatewayId = charge.Id
        };

        switch (charge.Source)
        {
            case Card card:
                {
                    transaction.PaymentMethodType = PaymentMethodType.Card;
                    transaction.Details = $"{card.Brand}, *{card.Last4}";
                    break;
                }
            case BankAccount bankAccount:
                {
                    transaction.PaymentMethodType = PaymentMethodType.BankAccount;
                    transaction.Details = $"{bankAccount.BankName}, *{bankAccount.Last4}";
                    break;
                }
            case Source { Card: not null } source:
                {
                    transaction.PaymentMethodType = PaymentMethodType.Card;
                    transaction.Details = $"{source.Card.Brand}, *{source.Card.Last4}";
                    break;
                }
            case Source { AchDebit: not null } source:
                {
                    transaction.PaymentMethodType = PaymentMethodType.BankAccount;
                    transaction.Details = $"{source.AchDebit.BankName}, *{source.AchDebit.Last4}";
                    break;
                }
            case Source source:
                {
                    if (source.AchCreditTransfer == null)
                    {
                        break;
                    }

                    var achCreditTransfer = source.AchCreditTransfer;

                    transaction.PaymentMethodType = PaymentMethodType.BankAccount;
                    transaction.Details = $"ACH => {achCreditTransfer.BankName}, {achCreditTransfer.AccountNumber}";

                    break;
                }
            default:
                {
                    if (charge.PaymentMethodDetails == null)
                    {
                        break;
                    }

                    if (charge.PaymentMethodDetails.Card != null)
                    {
                        var card = charge.PaymentMethodDetails.Card;
                        transaction.PaymentMethodType = PaymentMethodType.Card;
                        transaction.Details = $"{card.Brand?.ToUpperInvariant()}, *{card.Last4}";
                    }
                    else if (charge.PaymentMethodDetails.AchDebit != null)
                    {
                        var achDebit = charge.PaymentMethodDetails.AchDebit;
                        transaction.PaymentMethodType = PaymentMethodType.BankAccount;
                        transaction.Details = $"{achDebit.BankName}, *{achDebit.Last4}";
                    }
                    else if (charge.PaymentMethodDetails.AchCreditTransfer != null)
                    {
                        var achCreditTransfer = charge.PaymentMethodDetails.AchCreditTransfer;
                        transaction.PaymentMethodType = PaymentMethodType.BankAccount;
                        transaction.Details = $"ACH => {achCreditTransfer.BankName}, {achCreditTransfer.AccountNumber}";
                    }

                    break;
                }
        }

        return transaction;
    }

    private async Task HandlePaymentMethodAttachedAsync(PaymentMethod paymentMethod)
    {
        if (paymentMethod is null)
        {
            _logger.LogWarning("Attempted to handle the event payment_method.attached but paymentMethod was null");
            return;
        }

        var subscriptionListOptions = new SubscriptionListOptions
        {
            Customer = paymentMethod.CustomerId,
            Status = StripeSubscriptionStatus.Unpaid,
            Expand = ["data.latest_invoice"]
        };

        StripeList<Subscription> unpaidSubscriptions;
        try
        {
            unpaidSubscriptions = await _stripeFacade.ListSubscriptions(subscriptionListOptions);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Attempted to get unpaid invoices for customer {CustomerId} but encountered an error while calling Stripe",
                paymentMethod.CustomerId);

            return;
        }

        foreach (var unpaidSubscription in unpaidSubscriptions)
        {
            await AttemptToPayOpenSubscriptionAsync(unpaidSubscription);
        }
    }

    private async Task AttemptToPayOpenSubscriptionAsync(Subscription unpaidSubscription)
    {
        var latestInvoice = unpaidSubscription.LatestInvoice;

        if (unpaidSubscription.LatestInvoice is null)
        {
            _logger.LogWarning(
                "Attempted to pay unpaid subscription {SubscriptionId} but latest invoice didn't exist",
                unpaidSubscription.Id);

            return;
        }

        if (latestInvoice.Status != StripeInvoiceStatus.Open)
        {
            _logger.LogWarning(
                "Attempted to pay unpaid subscription {SubscriptionId} but latest invoice wasn't \"open\"",
                unpaidSubscription.Id);

            return;
        }

        try
        {
            await AttemptToPayInvoiceAsync(latestInvoice, true);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Attempted to pay open invoice {InvoiceId} on unpaid subscription {SubscriptionId} but encountered an error",
                latestInvoice.Id, unpaidSubscription.Id);
            throw;
        }
    }

    private static Tuple<Guid?, Guid?> GetIdsFromMetaData(Dictionary<string, string> metaData)
    {
        if (metaData == null || metaData.Count == 0)
        {
            return new Tuple<Guid?, Guid?>(null, null);
        }

        Guid? orgId = null;
        Guid? userId = null;

        if (metaData.TryGetValue("organizationId", out var orgIdString))
        {
            orgId = new Guid(orgIdString);
        }
        else if (metaData.TryGetValue("userId", out var userIdString))
        {
            userId = new Guid(userIdString);
        }

        if (userId != null && userId != Guid.Empty || orgId != null && orgId != Guid.Empty)
        {
            return new Tuple<Guid?, Guid?>(orgId, userId);
        }

        var orgIdKey = metaData.Keys
            .FirstOrDefault(k => k.Equals("organizationid", StringComparison.InvariantCultureIgnoreCase));

        if (!string.IsNullOrWhiteSpace(orgIdKey))
        {
            orgId = new Guid(metaData[orgIdKey]);
        }
        else
        {
            var userIdKey = metaData.Keys
                .FirstOrDefault(k => k.Equals("userid", StringComparison.InvariantCultureIgnoreCase));

            if (!string.IsNullOrWhiteSpace(userIdKey))
            {
                userId = new Guid(metaData[userIdKey]);
            }
        }

        return new Tuple<Guid?, Guid?>(orgId, userId);
    }

    private static bool OrgPlanForInvoiceNotifications(Organization org) => StaticStore.GetPlan(org.PlanType).IsAnnual;

    private async Task<bool> AttemptToPayInvoiceAsync(Invoice invoice, bool attemptToPayWithStripe = false)
    {
        var customer = await _stripeFacade.GetCustomer(invoice.CustomerId);

        if (customer?.Metadata?.ContainsKey("btCustomerId") ?? false)
        {
            return await AttemptToPayInvoiceWithBraintreeAsync(invoice, customer);
        }

        if (attemptToPayWithStripe)
        {
            return await AttemptToPayInvoiceWithStripeAsync(invoice);
        }

        return false;
    }

    private async Task<bool> AttemptToPayInvoiceWithBraintreeAsync(Invoice invoice, Customer customer)
    {
        _logger.LogDebug("Attempting to pay invoice with Braintree");
        if (!customer?.Metadata?.ContainsKey("btCustomerId") ?? true)
        {
            _logger.LogWarning(
                "Attempted to pay invoice with Braintree but btCustomerId wasn't on Stripe customer metadata");
            return false;
        }

        var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);
        var ids = GetIdsFromMetaData(subscription?.Metadata);
        if (!ids.Item1.HasValue && !ids.Item2.HasValue)
        {
            _logger.LogWarning(
                "Attempted to pay invoice with Braintree but Stripe subscription metadata didn't contain either a organizationId or userId");
            return false;
        }

        var orgTransaction = ids.Item1.HasValue;
        var btObjIdField = orgTransaction ? "organization_id" : "user_id";
        var btObjId = ids.Item1 ?? ids.Item2.Value;
        var btInvoiceAmount = (invoice.AmountDue / 100M);

        var existingTransactions = orgTransaction ?
            await _transactionRepository.GetManyByOrganizationIdAsync(ids.Item1.Value) :
            await _transactionRepository.GetManyByUserIdAsync(ids.Item2.Value);
        var duplicateTimeSpan = TimeSpan.FromHours(24);
        var now = DateTime.UtcNow;
        var duplicateTransaction = existingTransactions?
            .FirstOrDefault(t => (now - t.CreationDate) < duplicateTimeSpan);
        if (duplicateTransaction != null)
        {
            _logger.LogWarning("There is already a recent PayPal transaction ({0}). " +
                "Do not charge again to prevent possible duplicate.", duplicateTransaction.GatewayId);
            return false;
        }

        Result<Braintree.Transaction> transactionResult;
        try
        {
            transactionResult = await _btGateway.Transaction.SaleAsync(
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
                                $"{btObjIdField}:{btObjId},region:{_globalSettings.BaseServiceUri.CloudRegion}"
                        }
                    },
                    CustomFields = new Dictionary<string, string>
                    {
                        [btObjIdField] = btObjId.ToString(),
                        ["region"] = _globalSettings.BaseServiceUri.CloudRegion
                    }
                });
        }
        catch (NotFoundException e)
        {
            _logger.LogError(e,
                "Attempted to make a payment with Braintree, but customer did not exist for the given btCustomerId present on the Stripe metadata");
            throw;
        }

        if (!transactionResult.IsSuccess())
        {
            if (invoice.AttemptCount < 4)
            {
                await _mailService.SendPaymentFailedAsync(customer.Email, btInvoiceAmount, true);
            }
            return false;
        }

        try
        {
            await _stripeFacade.UpdateInvoice(invoice.Id, new InvoiceUpdateOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["btTransactionId"] = transactionResult.Target.Id,
                    ["btPayPalTransactionId"] =
                        transactionResult.Target.PayPalDetails?.AuthorizationId
                }
            });
            await _stripeFacade.PayInvoice(invoice.Id, new InvoicePayOptions { PaidOutOfBand = true });
        }
        catch (Exception e)
        {
            await _btGateway.Transaction.RefundAsync(transactionResult.Target.Id);
            if (e.Message.Contains("Invoice is already paid"))
            {
                await _stripeFacade.UpdateInvoice(invoice.Id, new InvoiceUpdateOptions
                {
                    Metadata = invoice.Metadata
                });
            }
            else
            {
                throw;
            }
        }

        return true;
    }

    private async Task<bool> AttemptToPayInvoiceWithStripeAsync(Invoice invoice)
    {
        try
        {
            await _stripeFacade.PayInvoice(invoice.Id);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogWarning(
                e,
                "Exception occurred while trying to pay Stripe invoice with Id: {InvoiceId}",
                invoice.Id);

            throw;
        }
    }

    private static bool ShouldAttemptToPayInvoice(Invoice invoice) =>
        invoice is
        {
            AmountDue: > 0,
            Paid: false,
            CollectionMethod: "charge_automatically",
            BillingReason: "subscription_cycle" or "automatic_pending_invoice_item_invoice",
            SubscriptionId: not null
        };

    private async Task<Subscription> VerifyCorrectTaxRateForCharge(Invoice invoice, Subscription subscription)
    {
        if (string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.Country) ||
            string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.PostalCode))
        {
            return subscription;
        }

        var localBitwardenTaxRates = await _taxRateRepository.GetByLocationAsync(
            new TaxRate()
            {
                Country = invoice.CustomerAddress.Country,
                PostalCode = invoice.CustomerAddress.PostalCode
            }
        );

        if (!localBitwardenTaxRates.Any())
        {
            return subscription;
        }

        var stripeTaxRate = await _stripeFacade.GetTaxRate(localBitwardenTaxRates.First().Id);
        if (stripeTaxRate == null || subscription.DefaultTaxRates.Any(x => x == stripeTaxRate))
        {
            return subscription;
        }

        subscription.DefaultTaxRates = [stripeTaxRate];

        var subscriptionOptions = new SubscriptionUpdateOptions { DefaultTaxRates = [stripeTaxRate.Id] };
        subscription = await _stripeFacade.UpdateSubscription(subscription.Id, subscriptionOptions);

        return subscription;
    }

    private static bool IsSponsoredSubscription(Subscription subscription) =>
        StaticStore.SponsoredPlans.Any(p => p.StripePlanId == subscription.Id);

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.PaymentFailed"/> event type from Stripe.
    /// </summary>
    /// <param name="invoice"></param>
    private async Task HandlePaymentFailedEventAsync(Invoice invoice)
    {
        if (!invoice.Paid && invoice.AttemptCount > 1 && ShouldAttemptToPayInvoice(invoice))
        {
            var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);
            // attempt count 4 = 11 days after initial failure
            if (invoice.AttemptCount <= 3 ||
                !subscription.Items.Any(i => i.Price.Id is PremiumPlanId or PremiumPlanIdAppStore))
            {
                await AttemptToPayInvoiceAsync(invoice);
            }
        }
    }

    private async Task CancelSubscription(string subscriptionId) =>
        await _stripeFacade.CancelSubscription(subscriptionId, new SubscriptionCancelOptions());

    private async Task VoidOpenInvoices(string subscriptionId)
    {
        var options = new InvoiceListOptions
        {
            Status = StripeInvoiceStatus.Open,
            Subscription = subscriptionId
        };
        var invoices = await _stripeFacade.ListInvoices(options);
        foreach (var invoice in invoices)
        {
            await _stripeFacade.VoidInvoice(invoice.Id);
        }
    }

    private string PickStripeWebhookSecret(string webhookBody)
    {
        var versionContainer = JsonSerializer.Deserialize<StripeWebhookVersionContainer>(webhookBody);

        return versionContainer.ApiVersion switch
        {
            "2023-10-16" => _billingSettings.StripeWebhookSecret20231016,
            "2022-08-01" => _billingSettings.StripeWebhookSecret,
            _ => HandleDefault(versionContainer.ApiVersion)
        };

        string HandleDefault(string version)
        {
            _logger.LogWarning(
                "Stripe webhook contained an recognized 'api_version': {ApiVersion}",
                version);

            return null;
        }
    }
}
