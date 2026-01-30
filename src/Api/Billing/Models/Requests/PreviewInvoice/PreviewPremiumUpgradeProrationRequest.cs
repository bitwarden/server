using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.PreviewInvoice;

public record PreviewPremiumUpgradeProrationRequest
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ProductTierType TargetProductTierType { get; set; }

    [Required]
    public required MinimalBillingAddressRequest BillingAddress { get; set; }

    private PlanType PlanType
    {
        get
        {
            if (TargetProductTierType is not (ProductTierType.Families or ProductTierType.Teams or ProductTierType.Enterprise))
            {
                throw new InvalidOperationException($"Cannot upgrade Premium subscription to {TargetProductTierType} plan.");
            }

            return TargetProductTierType switch
            {
                ProductTierType.Families => PlanType.FamiliesAnnually,
                ProductTierType.Teams => PlanType.TeamsAnnually,
                ProductTierType.Enterprise => PlanType.EnterpriseAnnually,
                _ => throw new InvalidOperationException($"Unexpected ProductTierType: {TargetProductTierType}")
            };
        }
    }

    public (PlanType, BillingAddress) ToDomain() =>
        (PlanType, BillingAddress.ToDomain());
}
