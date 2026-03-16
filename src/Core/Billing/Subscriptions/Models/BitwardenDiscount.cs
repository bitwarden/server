using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Models;

/// <summary>
/// The type of discounts Bitwarden supports.
/// </summary>
public enum BitwardenDiscountType
{
    [EnumMember(Value = "amount-off")]
    AmountOff,

    [EnumMember(Value = "percent-off")]
    PercentOff
}

/// <summary>
/// A record representing a discount applied to a Bitwarden subscription.
/// </summary>
public record BitwardenDiscount
{
    /// <summary>
    /// The type of the discount.
    /// </summary>
    [JsonConverter(typeof(EnumMemberJsonConverter<BitwardenDiscountType>))]
    public required BitwardenDiscountType Type { get; init; }

    /// <summary>
    /// The value of the discount.
    /// </summary>
    public required decimal Value { get; init; }

    public static implicit operator BitwardenDiscount(Discount? discount)
    {
        if (discount is not
            {
                Coupon.Valid: true
            })
        {
            return null!;
        }

        return discount.Coupon switch
        {
            { AmountOff: > 0 } => new BitwardenDiscount
            {
                Type = BitwardenDiscountType.AmountOff,
                Value = discount.Coupon.AmountOff.Value
            },
            { PercentOff: > 0 } => new BitwardenDiscount
            {
                Type = BitwardenDiscountType.PercentOff,
                Value = discount.Coupon.PercentOff.Value
            },
            _ => null!
        };
    }
}
