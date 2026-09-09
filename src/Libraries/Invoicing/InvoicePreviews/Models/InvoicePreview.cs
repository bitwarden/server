using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Utilities;

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>A structured projection of a Stripe invoice. Every monetary value originates from the invoice, not from local subscription or form state.</summary>
public record InvoicePreview
{
    public required PasswordManagerInvoiceItems PasswordManager { get; init; }
    public SecretsManagerInvoiceItems? SecretsManager { get; init; }

    [JsonConverter(typeof(EnumMemberJsonConverter<PlanCadenceType>))]
    public required PlanCadenceType Cadence { get; init; }

    [JsonConverter(typeof(EnumMemberJsonConverter<PlanTierType>))]
    public required PlanTierType PlanTier { get; init; }

    /// <summary>Cart-wide discounts (coupons with no applies-to set).</summary>
    public InvoicePreviewDiscount[]? Discounts { get; init; }

    /// <summary>Customer credit in dollars, carried only when negative. Not rendered in this epic.</summary>
    public decimal? StartingBalance { get; init; }

    /// <summary>Estimated tax on the whole invoice, in dollars.</summary>
    public required decimal EstimatedTax { get; init; }

    /// <summary>Invoice total in dollars, tax included.</summary>
    public required decimal Total { get; init; }

    /// <summary>What the subscriber is actually charged, in dollars. Differs from Total when customer credit applies.</summary>
    public required decimal AmountDue { get; init; }

    /// <summary>Null from the projection; set downstream from the subscription's current period end, not the invoice's next payment attempt (they diverge during dunning).</summary>
    public DateTime? NextPaymentAttempt { get; init; }
}
