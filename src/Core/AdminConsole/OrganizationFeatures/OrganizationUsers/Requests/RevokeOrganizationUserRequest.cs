using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public record RevokeOrganizationUsersRequest(
    Guid OrganizationId,
    IEnumerable<OrganizationUserUserDetails> OrganizationUsers,
    IActingUser ActionPerformedBy,
    RevocationReason RevocationReason)
{
    public RevokeOrganizationUsersRequest(Guid organizationId, OrganizationUserUserDetails organizationUser, IActingUser actionPerformedBy, RevocationReason revocationReason)
        : this(organizationId, [organizationUser], actionPerformedBy, revocationReason) { }
}
