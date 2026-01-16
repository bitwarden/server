using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class UpgradePremiumToOrganizationRequest
{
    [Required]
    public string OrganizationName { get; set; } = null!;

    [Required]
    public string Key { get; set; } = null!;

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductTierType Tier { get; set; }

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlanCadenceType Cadence { get; set; }

    private PlanType PlanType =>
        Tier switch
        {
            ProductTierType.Families => PlanType.FamiliesAnnually,
            ProductTierType.Teams => Cadence == PlanCadenceType.Monthly
                ? PlanType.TeamsMonthly
                : PlanType.TeamsAnnually,
            ProductTierType.Enterprise => Cadence == PlanCadenceType.Monthly
                ? PlanType.EnterpriseMonthly
                : PlanType.EnterpriseAnnually,
            _ => throw new InvalidOperationException("Cannot upgrade to an Organization subscription that isn't Families, Teams or Enterprise.")
        };

    public (string OrganizationName, string Key, PlanType PlanType) ToDomain() => (OrganizationName, Key, PlanType);
}
