using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public record RevokeOrganizationUsersRequest(
    Guid OrganizationId,
    ICollection<Guid> OrganizationUserIdsToRevoke,
    IActingUser PerformedBy
);

public record RevokeOrganizationUsersValidationRequest(
    Guid OrganizationId,
    ICollection<OrganizationUser> OrganizationUsersToRevoke,
    IActingUser PerformedBy
)
{
    public ICollection<Guid> OrganizationUserIdsToRevoke => OrganizationUsersToRevoke.Select(x => x.Id).ToArray();
}
