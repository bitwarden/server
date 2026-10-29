using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Premium.Models;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class UpgradePremiumToOrganizationRequest
{
    [Required]
    public string OrganizationName { get; set; } = null!;

    [Required]
    public string Key { get; set; } = null!;

    [Required]
    public string PublicKey { get; set; } = null!;

    [Required]
    public string EncryptedPrivateKey { get; set; } = null!;

    public string? CollectionName { get; set; }

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ProductTierType TargetProductTierType { get; set; }

    [Required]
    public required CheckoutBillingAddressRequest BillingAddress { get; set; }

    public DateTime? ProrationDate { get; set; }

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

    public PremiumToOrganizationUpgrade ToDomain() => new()
    {
        OrganizationName = OrganizationName,
        Key = Key,
        PublicKey = PublicKey,
        EncryptedPrivateKey = EncryptedPrivateKey,
        CollectionName = CollectionName,
        TargetPlanType = PlanType,
        BillingAddress = BillingAddress.ToDomain(),
        ProrationDate = ProrationDate
    };
}
