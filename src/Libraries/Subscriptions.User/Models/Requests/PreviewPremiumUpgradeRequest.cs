using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;

namespace Bit.Subscriptions.User.Models.Requests;

internal record PreviewPremiumUpgradeRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ProductTierType TargetProductTierType { get; init; }

    public required PremiumUpgradeBillingAddressRequest BillingAddress { get; init; }
}

internal record PremiumUpgradeBillingAddressRequest
{
    public required string Country { get; init; }

    public required string PostalCode { get; init; }
}
