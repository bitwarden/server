using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

/// <summary>
/// One line of a reconciliation export: an entitlement as the partner sees it. Carries the opaque
/// <see cref="AccountRef"/> and never the bound account's identity.
/// </summary>
public record PartnershipReconciliationExportRecord(
    string ExternalId,
    PartnershipEntitlementState State,
    SponsoredPlanType PlanType,
    Guid? AccountRef,
    IDictionary<string, string> Metadata,
    DateTime? BoundAt,
    DateTime? SuspendedAt,
    DateTime? CanceledAt,
    DateTime? ResumeWindowExpiresAt,
    DateTime LastAppliedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static PartnershipReconciliationExportRecord From(OrganizationPartnershipEntitlement entitlement) =>
        new(
            entitlement.ExternalId,
            entitlement.State,
            entitlement.SponsoredPlanType,
            entitlement.AccountRef,
            entitlement.GetMetadata(),
            AsUtc(entitlement.BoundDate),
            AsUtc(entitlement.SuspendedDate),
            AsUtc(entitlement.CanceledDate),
            AsUtc(entitlement.ResumeWindowExpirationDate),
            AsUtc(entitlement.LastAppliedEffectiveDate),
            AsUtc(entitlement.CreationDate),
            AsUtc(entitlement.RevisionDate));

    // Stored dates are UTC but read back unspecified; marking them UTC serializes them with a Z offset.
    private static DateTime AsUtc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? date) => date is { } value ? AsUtc(value) : null;
}
