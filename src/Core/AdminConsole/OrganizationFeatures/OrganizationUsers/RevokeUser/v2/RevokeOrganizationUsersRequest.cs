using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public class RevokeOrganizationUsersRequest
{
    public required Guid OrganizationId { get; init; }
    public required ICollection<Guid> OrganizationUserIdsToRevoke { get; init; } = [];
    public required IActingUser PerformedBy { get; init; }
}

public class RevokeOrganizationUsersValidationRequest : RevokeOrganizationUsersRequest
{
    public ICollection<OrganizationUser> OrganizationUsersToRevoke { get; init; } = [];
    public Organization? Organization { get; init; }
}
