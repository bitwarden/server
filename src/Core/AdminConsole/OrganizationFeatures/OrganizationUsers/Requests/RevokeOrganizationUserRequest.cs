using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public record RevokeOrganizationUsersRequest(
    Guid OrganizationId,
    IEnumerable<OrganizationUserUserDetails> OrganizationUsers,
    IActingUser ActionPerformedBy)
{
    public RevokeOrganizationUsersRequest(Guid organizationId, OrganizationUserUserDetails organizationUser, IActingUser actionPerformedBy)
        : this(organizationId, [organizationUser], actionPerformedBy) { }
}
