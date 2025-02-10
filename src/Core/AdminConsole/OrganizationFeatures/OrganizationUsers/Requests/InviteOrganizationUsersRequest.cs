using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public record InviteOrganizationUsersRequest
{
    public OrganizationUserInvite[] Invites { get; } = [];
    public OrganizationDto Organization { get; }
    public Guid PerformedBy { get; }
    public DateTimeOffset PerformedAt { get; }

    private InviteOrganizationUsersRequest(OrganizationUserInvite[] Invites,
        OrganizationDto Organization,
        Guid PerformedBy,
        DateTimeOffset PerformedAt)
    {
        this.Invites = Invites;
        this.Organization = Organization;
        this.PerformedBy = PerformedBy;
        this.PerformedAt = PerformedAt;
    }

    public static InviteOrganizationUsersRequest Create(
        IEnumerable<(Bit.Core.Models.Business.OrganizationUserInvite invite, string externalId)> invites,
        OrganizationDto organization, Guid performedBy, DateTimeOffset performedAt) =>
        new(invites.Select(inviteTuple =>
                OrganizationUserInvite.Create(
                    inviteTuple.invite.Emails.ToArray(),
                    inviteTuple.invite.Collections,
                    inviteTuple.invite.Type ?? OrganizationUserType.User,
                    inviteTuple.invite.Permissions,
                    inviteTuple.externalId)).ToArray(),
            organization,
            performedBy,
            performedAt);

    public static InviteOrganizationUsersRequest Create(InviteOrganizationUserRequest request) =>
        new([OrganizationUserInvite.Create(request.Invite)],
            request.Organization,
            request.PerformedBy,
            request.PerformedAt);

    public static InviteOrganizationUsersRequest Create(InviteScimOrganizationUserRequest request) =>
        new([
                OrganizationUserInvite.Create(request.Invite)
            ], request.Organization,
            Guid.Empty,
            request.PerformedAt);
}
