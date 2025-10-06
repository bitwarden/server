using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;

namespace Bit.Api.Billing.Models.Requests.Organizations;

public record OrganizationSubscriptionPlanChangeRequest : IValidatableObject
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductTierType Tier { get; set; }

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlanCadenceType Cadence { get; set; }

    public OrganizationSubscriptionPlanChange ToDomain() => new()
    {
        Tier = Tier,
        Cadence = Cadence
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Tier == ProductTierType.Families && Cadence == PlanCadenceType.Monthly)
        {
            yield return new ValidationResult("Monthly billing cadence is not available for the Families plan.");
        }
    }
}
