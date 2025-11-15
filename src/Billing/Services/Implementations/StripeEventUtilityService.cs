// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Billing.Constants;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Braintree;
using Stripe;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;
using Transaction = Bit.Core.Entities.Transaction;
using TransactionType = Bit.Core.Enums.TransactionType;

namespace Bit.Billing.Services.Implementations;

public class StripeEventUtilityService : IStripeEventUtilityService
{
    private readonly IStripeFacade _stripeFacade;
    private readonly ILogger<StripeEventUtilityService> _logger;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMailService _mailService;
    private readonly BraintreeGateway _btGateway;
    private readonly GlobalSettings _globalSettings;

    public StripeEventUtilityService(
        IStripeFacade stripeFacade,
        ILogger<StripeEventUtilityService> logger,
        ITransactionRepository transactionRepository,
        IMailService mailService,
        GlobalSettings globalSettings)
    {
        _stripeFacade = stripeFacade;
        _logger = logger;
        _transactionRepository = transactionRepository;
        _mailService = mailService;
        _btGateway = new BraintreeGateway
        {
            Environment = globalSettings.Braintree.Production ?
                Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
            MerchantId = globalSettings.Braintree.MerchantId,
            PublicKey = globalSettings.Braintree.PublicKey,
            PrivateKey = globalSettings.Braintree.PrivateKey
        };
        _globalSettings = globalSettings;
    }

    /// <summary>
    /// Gets the organizationId, userId, or providerId from the metadata of a Stripe Subscription object.
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public Tuple<Guid?, Guid?, Guid?> GetIdsFromMetadata(Dictionary<string, string> metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return new Tuple<Guid?, Guid?, Guid?>(null, null, null);
        }

        metadata.TryGetValue("organizationId", out var orgIdString);
        metadata.TryGetValue("userId", out var userIdString);
        metadata.TryGetValue("providerId", out var providerIdString);

        orgIdString ??= metadata.FirstOrDefault(x =>
            x.Key.Equals("organizationId", StringComparison.OrdinalIgnoreCase)).Value;

        userIdString ??= metadata.FirstOrDefault(x =>
            x.Key.Equals("userId", StringComparison.OrdinalIgnoreCase)).Value;

        providerIdString ??= metadata.FirstOrDefault(x =>
            x.Key.Equals("providerId", StringComparison.OrdinalIgnoreCase)).Value;

        Guid? organizationId = string.IsNullOrWhiteSpace(orgIdString) ? null : new Guid(orgIdString);
        Guid? userId = string.IsNullOrWhiteSpace(userIdString) ? null : new Guid(userIdString);
        Guid? providerId = string.IsNullOrWhiteSpace(providerIdString) ? null : new Guid(providerIdString);

        return new Tuple<Guid?, Guid?, Guid?>(organizationId, userId, providerId);
    }

    /// <summary>
    /// Gets the organization or user ID from the metadata of a Stripe Charge object.
    /// </summary>
    /// <param name="charge"></param>
    /// <returns></returns>
    public async Task<(Guid?, Guid?, Guid?)> GetEntityIdsFromChargeAsync(Charge charge)
    {
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

            var (organizationId, userId, providerId) = GetIdsFromMetadata(subscription.Metadata);

            if (organizationId.HasValue || userId.HasValue || providerId.HasValue)
            {
                return (organizationId, userId, providerId);
            }
        }

        return (null, null, null);
    }

    public bool IsSponsoredSubscription(Subscription subscription) =>
        SponsoredPlans.All
            .Any(p => subscription.Items
                .Any(i => i.Plan.Id == p.StripePlanId));

    /// <summary>
    /// Converts a Stripe Charge object to a Bitwarden Transaction object.
    /// </summary>
    /// <param name="charge"></param>
    /// <param name="organizationId"></param>
    /// <param name="userId"></param>
    /// /// <param name="providerId"></param>
    /// <returns></returns>
    public Transaction FromChargeToTransaction(Charge charge, Guid? organizationId, Guid? userId, Guid? providerId)
    {
        var transaction = new Transaction
        {
            Amount = charge.Amount / 100M,
            CreationDate = charge.Created,
            OrganizationId = organizationId,
            UserId = userId,
            ProviderId = providerId,
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
                    else if (charge.PaymentMethodDetails.UsBankAccount != null)
                    {
                        var usBankAccount = charge.PaymentMethodDetails.UsBankAccount;
                        transaction.PaymentMethodType = PaymentMethodType.BankAccount;
                        transaction.Details = $"{usBankAccount.BankName}, *{usBankAccount.Last4}";
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

    public async Task<bool> AttemptToPayInvoiceAsync(Invoice invoice, bool attemptToPayWithStripe = false)
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

    public bool ShouldAttemptToPayInvoice(Invoice invoice) =>
        invoice is
        {
            AmountDue: > 0,
            Status: not StripeConstants.InvoiceStatus.Paid,
            CollectionMethod: "charge_automatically",
            BillingReason: "subscription_cycle" or "automatic_pending_invoice_item_invoice",
            Parent.SubscriptionDetails: not null
        };

    private async Task<bool> AttemptToPayInvoiceWithBraintreeAsync(Invoice invoice, Customer customer)
    {
        _logger.LogDebug("Attempting to pay invoice with Braintree");
        if (!customer?.Metadata?.ContainsKey("btCustomerId") ?? true)
        {
            _logger.LogWarning(
                "Attempted to pay invoice with Braintree but btCustomerId wasn't on Stripe customer metadata");
            return false;
        }

        if (invoice.Parent?.SubscriptionDetails == null)
        {
            _logger.LogWarning("Invoice parent was not a subscription.");
            return false;
        }

        var subscription = await _stripeFacade.GetSubscription(invoice.Parent.SubscriptionDetails.SubscriptionId);
        var (organizationId, userId, providerId) = GetIdsFromMetadata(subscription?.Metadata);
        if (!organizationId.HasValue && !userId.HasValue && !providerId.HasValue)
        {
            _logger.LogWarning(
                "Attempted to pay invoice with Braintree but Stripe subscription metadata didn't contain either a organizationId or userId or ");
            return false;
        }

        var orgTransaction = organizationId.HasValue;
        string btObjIdField;
        Guid btObjId;
        if (organizationId.HasValue)
        {
            btObjIdField = "organization_id";
            btObjId = organizationId.Value;
        }
        else if (userId.HasValue)
        {
            btObjIdField = "user_id";
            btObjId = userId.Value;
        }
        else
        {
            btObjIdField = "provider_id";
            btObjId = providerId.Value;
        }
        var btInvoiceAmount = Math.Round(invoice.AmountDue / 100M, 2);

        var existingTransactions = organizationId.HasValue
            ? await _transactionRepository.GetManyByOrganizationIdAsync(organizationId.Value)
            : userId.HasValue
                ? await _transactionRepository.GetManyByUserIdAsync(userId.Value)
                : await _transactionRepository.GetManyByProviderIdAsync(providerId.Value);

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
            var transactionRequest = new Braintree.TransactionRequest
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
            };

            _logger.LogInformation("Creating Braintree transaction with Amount: {Amount}, CustomerId: {CustomerId}, " +
                "CustomField: {CustomField}, CustomFields: {@CustomFields}",
                transactionRequest.Amount,
                transactionRequest.CustomerId,
                transactionRequest.Options.PayPal.CustomField,
                transactionRequest.CustomFields);

            transactionResult = await _btGateway.Transaction.SaleAsync(transactionRequest);
        }
        catch (NotFoundException e)
        {
            _logger.LogError(e,
                "Attempted to make a payment with Braintree, but customer did not exist for the given btCustomerId present on the Stripe metadata");
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception occurred while trying to pay invoice with Braintree");
            throw;
        }

        if (!transactionResult.IsSuccess())
        {
            _logger.LogWarning("Braintree transaction failed. Error: {ErrorMessage}, Transaction Status: {Status}, Validation Errors: {ValidationErrors}",
                transactionResult.Message,
                transactionResult.Target?.Status,
                string.Join(", ", transactionResult.Errors.DeepAll().Select(e => $"Code: {e.Code}, Message: {e.Message}, Attribute: {e.Attribute}")));

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
}
