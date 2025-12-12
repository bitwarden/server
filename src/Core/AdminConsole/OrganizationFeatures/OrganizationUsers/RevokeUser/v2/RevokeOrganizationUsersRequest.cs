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
    ICollection<Guid> OrganizationUserIdsToRevoke,
    IActingUser PerformedBy,
    ICollection<OrganizationUser> OrganizationUsersToRevoke
) : RevokeOrganizationUsersRequest(OrganizationId, OrganizationUserIdsToRevoke, PerformedBy);
