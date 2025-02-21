using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersRequest
{
    public OrganizationUserInvite[] Invites { get; } = [];
    public OrganizationDto Organization { get; }
    public Guid PerformedBy { get; }
    public DateTimeOffset PerformedAt { get; }

    public InviteOrganizationUsersRequest(OrganizationUserInvite[] Invites,
        OrganizationDto Organization,
        Guid PerformedBy,
        DateTimeOffset PerformedAt)
    {
        this.Invites = Invites;
        this.Organization = Organization;
        this.PerformedBy = PerformedBy;
        this.PerformedAt = PerformedAt;
    }

    public static InviteOrganizationUsersRequest Create(InviteScimOrganizationUserRequest request) =>
        new([OrganizationUserInvite.Create(request.Invite, request.ExternalId)],
            request.Organization,
            Guid.Empty,
            request.PerformedAt);
}
