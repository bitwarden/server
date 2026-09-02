using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;

public record AcceptOrganizationMembershipValidationRequest
{
    public required Guid OrganizationId { get; init; }
    public required User User { get; init; }
    public required ICollection<OrganizationUser> AllOrganizationMemberships { get; init; }
    public OrganizationUser? ExistingMembership { get; init; }
}
