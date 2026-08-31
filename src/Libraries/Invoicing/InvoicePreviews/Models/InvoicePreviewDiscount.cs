using System.Text.Json.Serialization;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Utilities;

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>A discount as the preview renders it.</summary>
public record InvoicePreviewDiscount
{
    [JsonConverter(typeof(EnumMemberJsonConverter<BitwardenDiscountType>))]
    public required BitwardenDiscountType Type { get; init; }

    /// <summary>Percent off from 0 to 100, or amount off in dollars.</summary>
    public required decimal Value { get; init; }

    /// <summary>The authoritative applied amount in dollars, taken from Stripe.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Coupon name, used as the row label.</summary>
    public string? Label { get; init; }
}
