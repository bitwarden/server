using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersRequest
{
    public OrganizationUserInvite[] Invites { get; } = [];
    public InviteOrganization InviteOrganization { get; }
    public Guid PerformedBy { get; }
    public DateTimeOffset PerformedAt { get; }

    public InviteOrganizationUsersRequest(OrganizationUserInvite[] invites,
        InviteOrganization inviteOrganization,
        Guid performedBy,
        DateTimeOffset performedAt)
    {
        Invites = invites;
        InviteOrganization = inviteOrganization;
        PerformedBy = performedBy;
        PerformedAt = performedAt;
    }
}
