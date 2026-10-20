using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

namespace Bit.Api.AdminConsole.Partnerships.Models.Request;

/// <summary>
/// Provisions an entitlement for one of the partner's customers.
/// </summary>
public class ProvisionEntitlementRequestModel
{
    /// <summary>
    /// The partner's opaque identifier for the customer, unique within the partnership.
    /// </summary>
    [Required]
    [StringLength(OrganizationPartnershipEntitlement.ExternalIdMaxLength)]
    public string ExternalId { get; set; } = null!;

    /// <summary>
    /// The sponsored plan, e.g. <c>premium</c>. Must be one the partnership allows. May be omitted when the partnership
    /// allows exactly one.
    /// </summary>
    [JsonConverter(typeof(SnakeCasePlanTypeConverter))]
    public SponsoredPlanType? PlanType { get; set; }

    /// <summary>
    /// A flat string map echoed back on reads, with at most 16 keys.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// When the partner provisioned the customer. Defaults to now and must not be in the future.
    /// </summary>
    public DateTimeOffset? EffectiveAt { get; set; }

    public ProvisionPartnershipEntitlementRequest ToRequest(Guid organizationPartnershipId) =>
        new()
        {
            OrganizationPartnershipId = organizationPartnershipId,
            ExternalId = ExternalId,
            SponsoredPlanType = PlanType,
            Metadata = Metadata,
            EffectiveAt = EffectiveAt?.UtcDateTime,
        };

    private sealed class SnakeCasePlanTypeConverter()
        : JsonStringEnumConverter<SponsoredPlanType>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false);
}
