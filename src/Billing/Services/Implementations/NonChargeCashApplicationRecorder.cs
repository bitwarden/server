using System.Globalization;
using Bit.Billing.Constants;
using Bit.Core.Billing.Constants;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Data.SqlClient;
using Stripe;
using PaymentMethodType = Bit.Core.Enums.PaymentMethodType;
using Transaction = Bit.Core.Entities.Transaction;
using TransactionType = Bit.Core.Enums.TransactionType;

namespace Bit.Billing.Services.Implementations;

/// <summary>
/// Posts non-charge cash application payments as Bitwarden <see cref="Transaction"/>s so they appear in payment
/// history. Cash application applies a check, wire or bank transfer to a Stripe invoice as a Payment Record (Stripe
/// Dashboard "Record off-Stripe payment", or the API). Stripe creates no charge, so <see cref="ChargeSucceededHandler"/>
/// never sees it. One Transaction is written per invoice payment, in the shape Billing Operations enters manually in
/// Admin: Type <see cref="TransactionType.Credit"/>, Gateway <see cref="GatewayType.Bank"/>.
/// </summary>
public class NonChargeCashApplicationRecorder(
    ILogger<NonChargeCashApplicationRecorder> logger,
    ILoggerFactory loggerFactory,
    IStripeFacade stripeFacade,
    ITransactionRepository transactionRepository) : INonChargeCashApplicationRecorder
{
    /// <summary>
    /// Audit log category with one line per posted payment. Off by default (<c>None</c> in appsettings.json); set
    /// <c>Logging:LogLevel:Bit.Billing.NonChargeCashApplicationAudit</c> to <c>Information</c> to turn it on, and
    /// <c>Logging:Console:LogLevel:Bit.Billing.NonChargeCashApplicationAudit</c> too where the console provider filters
    /// at a higher level, as in appsettings.Production.json.
    /// </summary>
    public const string AuditLogCategory = "Bit.Billing.NonChargeCashApplicationAudit";

    public const string PaymentRecordPaymentType = "payment_record";

    private const string CustomType = "custom";
    private const string PaidStatus = "paid";
    private const string SupportedCurrency = "usd";
    private const int DetailsMaxLength = 100;
    private const string DateSeparator = " - ";
    private const int GatewayIdMaxLength = 50;
    private const int GuidLength = 36;
    private const int SqlForeignKeyViolation = 547;
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    private static readonly CultureInfo _usCulture = CultureInfo.GetCultureInfo("en-US");

    private static readonly string[] _ownerKeysByPrecedence =
    [
        StripeConstants.MetadataKeys.OrganizationId,
        StripeConstants.MetadataKeys.UserId,
        StripeConstants.MetadataKeys.ProviderId
    ];

    private readonly ILogger _auditLogger = loggerFactory.CreateLogger(AuditLogCategory);

    public async Task RecordAsync(InvoicePayment invoicePayment)
    {
        // Card and bank-debit payments are Stripe charges and are recorded by ChargeSucceededHandler.
        if (invoicePayment.Payment?.Type != PaymentRecordPaymentType ||
            string.IsNullOrEmpty(invoicePayment.Payment.PaymentRecordId) ||
            invoicePayment.Status != PaidStatus ||
            (invoicePayment.AmountPaid ?? 0) <= 0)
        {
            return;
        }

        // Transaction has no currency; never record a foreign amount as dollars.
        if (!string.Equals(invoicePayment.Currency, SupportedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(
                "Non-charge cash application payment {InvoicePaymentId} is in {Currency}, only {SupportedCurrency} is supported; no transaction created",
                invoicePayment.Id, invoicePayment.Currency, SupportedCurrency);
            return;
        }

        if (invoicePayment.Id.Length > GatewayIdMaxLength)
        {
            logger.LogError(
                "Invoice payment ID {InvoicePaymentId} is longer than {MaxLength} characters; no transaction created",
                invoicePayment.Id, GatewayIdMaxLength);
            return;
        }

        var amount = invoicePayment.AmountPaid!.Value / 100M;

        var existingTransaction = await transactionRepository.GetByGatewayIdAsync(GatewayType.Bank, invoicePayment.Id);
        if (existingTransaction is not null)
        {
            ReportExisting(existingTransaction, invoicePayment.Id, amount);
            return;
        }

        var paymentRecord = await stripeFacade.GetPaymentRecord(invoicePayment.Payment.PaymentRecordId);
        if (!IsNonChargeCashApplicationPayment(paymentRecord))
        {
            logger.LogInformation(
                "Payment record {PaymentRecordId} for invoice payment {InvoicePaymentId} is not a non-charge cash application payment; skipping",
                paymentRecord.Id, invoicePayment.Id);
            return;
        }

        // Fees (e.g. a wire fee deducted by the bank) are applied to settle the invoice but are not money received.
        if (IsFee(paymentRecord.PaymentMethodDetails?.Custom?.DisplayName))
        {
            logger.LogInformation(
                "Payment record {PaymentRecordId} for invoice payment {InvoicePaymentId} is a fee, not money received; skipping",
                paymentRecord.Id, invoicePayment.Id);
            return;
        }

        var owner = await GetOwnerAsync(invoicePayment);
        if (owner is null)
        {
            return;
        }

        var (organizationId, userId, providerId) = owner.Value;
        var paymentMethodType = GetPaymentMethodType(paymentRecord);

        var transaction = new Transaction
        {
            Amount = amount,
            // Stripe's exact UTC paid_at, as card charges use charge.Created; a back-dated Dashboard "Effective date"
            // is midnight US Eastern.
            CreationDate = invoicePayment.StatusTransitions?.PaidAt ?? invoicePayment.Created,
            OrganizationId = organizationId,
            UserId = userId,
            ProviderId = providerId,
            Type = TransactionType.Credit,
            Gateway = GatewayType.Bank,
            GatewayId = invoicePayment.Id,
            PaymentMethodType = paymentMethodType,
            Details = GetDetails(paymentRecord, paymentMethodType, invoicePayment.Id, amount)
        };

        try
        {
            await transactionRepository.CreateAsync(transaction);

            _auditLogger.LogInformation(
                "Posted non-charge cash application payment {InvoicePaymentId} on invoice {InvoiceId} as transaction {TransactionId} for organization {OrganizationId} / user {UserId} / provider {ProviderId}",
                invoicePayment.Id, invoicePayment.InvoiceId, transaction.Id, organizationId, userId, providerId);
        }
        catch (SqlException e) when (e.Number is SqlUniqueIndexViolation or SqlUniqueConstraintViolation)
        {
            // Another delivery of the same payment inserted it first.
            var concurrentTransaction = await transactionRepository.GetByGatewayIdAsync(GatewayType.Bank, invoicePayment.Id);
            if (concurrentTransaction is null)
            {
                throw;
            }

            ReportExisting(concurrentTransaction, invoicePayment.Id, amount);
        }
        catch (SqlException e) when (e.Number == SqlForeignKeyViolation)
        {
            logger.LogError(
                "Non-charge cash application payment {InvoicePaymentId} could not be recorded; the organization, user or provider may have been deleted",
                invoicePayment.Id);
        }
    }

    /// <summary>
    /// Payment Records also represent payments Stripe processed; only a custom payment method or processor means the
    /// money arrived with no Stripe charge. Excludes the record Stripe generates itself when an invoice is paid out of
    /// band with no payment details (custom payment method without a custom type, "Paid out of band"), which is how
    /// Bitwarden's PayPal flow settles invoices; cash application always records the payment details.
    /// </summary>
    private static bool IsNonChargeCashApplicationPayment(PaymentRecord paymentRecord)
    {
        var paymentMethod = paymentRecord.PaymentMethodDetails;
        if (paymentMethod?.Type == CustomType && string.IsNullOrEmpty(paymentMethod.Custom?.Type))
        {
            return false;
        }

        return paymentMethod?.Type == CustomType || paymentRecord.ProcessorDetails?.Type == CustomType;
    }

    /// <summary>
    /// The owner from the metadata Stripe copied onto the invoice, else the invoice's own subscription (even if since
    /// canceled), else, as for card charges, the first of the customer's subscriptions that is not canceled and has
    /// an owner.
    /// </summary>
    private async Task<(Guid?, Guid?, Guid?)?> GetOwnerAsync(InvoicePayment invoicePayment)
    {
        var invoice = invoicePayment.Invoice;
        var subscriptionDetails = invoice?.Parent?.SubscriptionDetails;

        if (TryGetOwner(subscriptionDetails?.Metadata, out var owner))
        {
            return owner;
        }

        if (!string.IsNullOrEmpty(subscriptionDetails?.SubscriptionId))
        {
            var invoiceSubscription = await stripeFacade.GetSubscription(subscriptionDetails.SubscriptionId);
            if (TryGetOwner(invoiceSubscription?.Metadata, out owner))
            {
                return owner;
            }
        }

        if (!string.IsNullOrEmpty(invoice?.CustomerId))
        {
            var subscriptions = await stripeFacade.ListSubscriptions(new SubscriptionListOptions
            {
                Customer = invoice.CustomerId
            });

            foreach (var subscription in subscriptions?.Data ?? [])
            {
                if (subscription.Status is StripeSubscriptionStatus.Canceled or StripeSubscriptionStatus.IncompleteExpired)
                {
                    continue;
                }

                if (TryGetOwner(subscription.Metadata, out owner))
                {
                    return owner;
                }
            }
        }

        logger.LogWarning(
            "Non-charge cash application payment {InvoicePaymentId} on invoice {InvoiceId}: no organization, user or provider on the invoice, its subscription or customer {CustomerId}'s subscriptions; no transaction created",
            invoicePayment.Id, invoicePayment.InvoiceId, invoice?.CustomerId);
        return null;
    }

    /// <summary>
    /// The first valid ID by precedence organizationId, userId, providerId; reseller providers are never the billing
    /// entity when an organization or user exists. Blank and malformed values are skipped, never thrown on.
    /// </summary>
    private static bool TryGetOwner(Dictionary<string, string>? metadata, out (Guid?, Guid?, Guid?) owner)
    {
        owner = default;

        if (metadata is not { Count: > 0 })
        {
            return false;
        }

        foreach (var key in _ownerKeysByPrecedence)
        {
            var value = metadata.FirstOrDefault(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrWhiteSpace(value) || !TryParseOwnerId(value, out var id))
            {
                continue;
            }

            owner = key switch
            {
                StripeConstants.MetadataKeys.OrganizationId => (id, null, null),
                StripeConstants.MetadataKeys.UserId => (null, id, null),
                _ => (null, null, id)
            };
            return true;
        }

        return false;
    }

    /// <summary>
    /// Billing sometimes appends text to a metadata ID so other webhook handlers leave the owner alone. Recording a
    /// payment still applies, so the ID is read from the first 36 characters; surrounding whitespace is ignored.
    /// </summary>
    private static bool TryParseOwnerId(string value, out Guid id)
    {
        var trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out id))
        {
            return true;
        }

        return trimmed.Length > GuidLength && Guid.TryParseExact(trimmed[..GuidLength], "D", out id);
    }

    /// <summary>
    /// A redelivered event never changes the saved row. A different amount means it was edited in Admin, e.g. to show
    /// the full amount of a receipt larger than the invoice.
    /// </summary>
    private void ReportExisting(Transaction existing, string invoicePaymentId, decimal amount)
    {
        if (existing.Amount != amount)
        {
            logger.LogWarning(
                "Non-charge cash application payment {InvoicePaymentId} is already recorded as transaction {TransactionId} with a different amount (edited in Admin?); left unchanged",
                invoicePaymentId, existing.Id);
            return;
        }

        logger.LogInformation(
            "Non-charge cash application payment {InvoicePaymentId} already recorded as transaction {TransactionId}",
            invoicePaymentId, existing.Id);
    }

    /// <summary>
    /// A fee when the payment type starts or ends with the word FEE ("Wire Fee", "Bank Fee", "Fee"), so a name that
    /// merely contains it ("Wire - Fee Associates LLC") is not one.
    /// </summary>
    private static bool IsFee(string? paymentType)
    {
        var words = Normalize(paymentType).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasFeeAtStart = words.Length > 0 && words[0] == "FEE";
        var hasFeeAtEnd = words.Length > 0 && words[^1] == "FEE";
        return hasFeeAtStart || hasFeeAtEnd;
    }

    /// <summary>
    /// From the payment type entered in Stripe: check or wire, otherwise bank account.
    /// </summary>
    private static PaymentMethodType GetPaymentMethodType(PaymentRecord paymentRecord)
    {
        var paymentType = Normalize(paymentRecord.PaymentMethodDetails?.Custom?.DisplayName);

        if (ContainsWord(paymentType, "CHECK", "CHEQUE", "CHK"))
        {
            return PaymentMethodType.Check;
        }

        return ContainsWord(paymentType, "WIRE") ? PaymentMethodType.WireTransfer : PaymentMethodType.BankAccount;
    }

    /// <summary>
    /// Matches manually entered transactions. Billing Operations enters the date received and the paying entity in
    /// the Payment Record's reference as "&lt;date&gt; - &lt;payer&gt;" (for a check, add "#&lt;check number&gt;"); the
    /// payment type and amount come from their own fields. "11/10/2026 - EXAMPLE PAYER LTD" on a $671.34 wire
    /// becomes "11/10/2026 In Wire - EXAMPLE PAYER LTD $671.34". When the reference ends with a different amount,
    /// such as the total of a wire that paid several invoices, it is kept as "of $5,000.00" after this payment's
    /// amount. Longer than the 100-character column, the payer is shortened.
    /// </summary>
    private string GetDetails(PaymentRecord paymentRecord, PaymentMethodType paymentMethodType, string invoicePaymentId,
        decimal amount)
    {
        var paymentType = paymentRecord.PaymentMethodDetails?.Custom?.DisplayName?.Trim();

        var label = paymentMethodType switch
        {
            PaymentMethodType.Check => "Check In",
            PaymentMethodType.WireTransfer => "In Wire",
            _ => string.IsNullOrEmpty(paymentType) ? "Bank Payment" : paymentType
        };

        var (date, payer, receivedTotal) = GetDateAndPayer(paymentRecord, paymentMethodType, amount);

        var description = string.IsNullOrEmpty(date) ? label : $"{date} {label}";
        if (!string.IsNullOrEmpty(payer))
        {
            description += $" - {payer}";
        }

        var suffix = $" {amount.ToString("C", _usCulture)}";
        if (receivedTotal is not null)
        {
            suffix += $" of {receivedTotal.Value.ToString("C", _usCulture)}";
        }

        var maxDescriptionLength = DetailsMaxLength - suffix.Length;
        if (description.Length > maxDescriptionLength)
        {
            logger.LogWarning(
                "Description for non-charge cash application payment {InvoicePaymentId} is {Length} characters and was shortened to fit transaction details",
                invoicePaymentId, description.Length + suffix.Length);
            description = Truncate(description, maxDescriptionLength).TrimEnd();
        }

        return $"{description}{suffix}";
    }

    /// <summary>
    /// The date received, the paying entity and any different total received, from the reference: the text
    /// before the first " - " is the date and the rest is the payer. A trailing "$&lt;amount&gt;" is removed from the
    /// payer and returned as the total received when it differs from this payment. A reference without " - " is
    /// the payer alone. A check reference that is only a check number, and a missing reference, fall back to the
    /// Stripe customer name.
    /// </summary>
    private static (string? Date, string? Payer, decimal? ReceivedTotal) GetDateAndPayer(PaymentRecord paymentRecord,
        PaymentMethodType paymentMethodType, decimal amount)
    {
        var customerName = paymentRecord.CustomerDetails?.Name?.Trim();
        var reference = paymentRecord.ProcessorDetails?.Custom?.PaymentReference?.Trim();
        decimal? receivedTotal = null;
        if (reference is not null && TrySplitTrailingAmount(reference, out var withoutAmount, out var trailingAmount))
        {
            reference = withoutAmount;
            receivedTotal = trailingAmount == amount ? null : trailingAmount;
        }

        if (string.IsNullOrEmpty(reference))
        {
            return (null, customerName, receivedTotal);
        }

        if (paymentMethodType == PaymentMethodType.Check && IsCheckNumber(reference))
        {
            var checkNumber = $"#{reference.TrimStart('#')}";
            return (null, string.IsNullOrEmpty(customerName) ? checkNumber : $"{customerName} {checkNumber}",
                receivedTotal);
        }

        if (reference.EndsWith(DateSeparator.TrimEnd(), StringComparison.Ordinal))
        {
            reference += " ";
        }

        var separator = reference.IndexOf(DateSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            return (null, reference, receivedTotal);
        }

        var date = reference[..separator].Trim();
        var payer = reference[(separator + DateSeparator.Length)..].Trim();
        return (date.Length == 0 ? null : date, payer.Length == 0 ? customerName : payer, receivedTotal);
    }

    /// <summary>
    /// Splits a final "$&lt;amount&gt;" word (e.g. "$671.34" or "$1,500") off the reference.
    /// </summary>
    private static bool TrySplitTrailingAmount(string reference, out string withoutAmount, out decimal trailingAmount)
    {
        withoutAmount = reference;
        trailingAmount = 0;

        var dollar = reference.LastIndexOf('$');
        if (dollar < 0 || (dollar > 0 && !char.IsWhiteSpace(reference[dollar - 1])))
        {
            return false;
        }

        if (!decimal.TryParse(reference[(dollar + 1)..].Trim(),
                NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, _usCulture, out trailingAmount))
        {
            return false;
        }

        withoutAmount = reference[..dollar].TrimEnd();
        return true;
    }

    private static bool IsCheckNumber(string reference)
    {
        var digits = reference.TrimStart('#');
        return digits.Length > 0 && digits.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Shortens without splitting a surrogate pair (e.g. an emoji in a customer name).
    /// </summary>
    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..(char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength)];

    private static string Normalize(string? value) =>
        new((value ?? string.Empty).ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());

    private static bool ContainsWord(string normalized, params string[] words) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(words.Contains);
}
