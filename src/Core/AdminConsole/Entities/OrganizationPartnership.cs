using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// A partner's configuration for sponsoring Bitwarden plans for their own customers, attached to an
/// <see cref="Organization"/> the partner operates.
/// </summary>
/// <remarks>
/// Sponsored customers are not members of the organization. They are linked to it through
/// <see cref="OrganizationPartnershipEntitlement"/> records.
/// </remarks>
public class OrganizationPartnership : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    [MaxLength(50)]
    public string Name { get; set; } = null!;
    public PartnershipStatus Status { get; set; }
    public SponsoredPlanType SponsoredPlanType { get; set; }
    public PartnershipBindingMode BindingMode { get; set; }
    /// <summary>
    /// JSON settings for <see cref="PartnershipBindingMode.Oidc"/>, such as the claim path holding the
    /// external identifier. Null in token mode.
    /// </summary>
    public string? IdentityBindingConfiguration { get; set; }
    /// <summary>
    /// A JSON-serialized list of origins an activation link's return URL must match.
    /// Use <see cref="GetRegisteredReturnOrigins"/> and <see cref="SetRegisteredReturnOrigins"/>.
    /// </summary>
    public string RegisteredReturnOrigins { get; set; } = "[]";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public IEnumerable<string> GetRegisteredReturnOrigins() =>
        JsonSerializer.Deserialize<IEnumerable<string>>(RegisteredReturnOrigins) ?? [];

    public void SetRegisteredReturnOrigins(IEnumerable<string> origins) =>
        RegisteredReturnOrigins = JsonSerializer.Serialize(origins);

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
