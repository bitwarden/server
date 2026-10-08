using Bit.Core.AdminConsole.Enums.Partnerships;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public record CreateOrganizationPartnershipRequest
{
    public const int NameMaxLength = 50;

    public required Guid OrganizationId { get; init; }
    public required string Name { get; init; }
    public required SponsoredPlanType SponsoredPlanType { get; init; }
    /// <summary>
    /// Only <see cref="PartnershipBindingMode.Token"/> is supported.
    /// </summary>
    public required PartnershipBindingMode BindingMode { get; init; }
    /// <summary>
    /// Absolute https origins (scheme, host, optional port) an activation link may return to.
    /// </summary>
    public required IEnumerable<string> RegisteredReturnOrigins { get; init; }
}
