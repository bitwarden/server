using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public record RevokeOrganizationUsersRequest(
    Guid OrganizationId,
    IEnumerable<IOrganizationUser> OrganizationUsers,
    IActingUser ActionPerformedBy)
{
    public RevokeOrganizationUsersRequest(Guid organizationId, IOrganizationUser organizationUser, IActingUser actionPerformedBy)
        : this(organizationId, [organizationUser], actionPerformedBy) { }
}
