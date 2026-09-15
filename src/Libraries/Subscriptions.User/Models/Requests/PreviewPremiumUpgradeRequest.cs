using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;

namespace Bit.Subscriptions.User.Models.Requests;

internal record PreviewPremiumUpgradeRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ProductTierType TargetProductTierType { get; init; }

    public required BillingAddressRequest BillingAddress { get; init; }
}

internal record BillingAddressRequest
{
    public required string Country { get; init; }

    public required string PostalCode { get; init; }
}
