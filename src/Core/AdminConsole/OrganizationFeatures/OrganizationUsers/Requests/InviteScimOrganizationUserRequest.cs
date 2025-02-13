using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public record InviteScimOrganizationUserRequest
{
    public OrganizationUserSingleEmailInvite Invite { get; }
    public OrganizationDto Organization { get; }
    public DateTimeOffset PerformedAt { get; }

    private InviteScimOrganizationUserRequest(OrganizationUserSingleEmailInvite Invite,
        OrganizationDto Organization,
        DateTimeOffset PerformedAt)
    {
        this.Invite = Invite;
        this.Organization = Organization;
        this.PerformedAt = PerformedAt;
    }

    public static InviteScimOrganizationUserRequest Create(OrganizationUserSingleEmailInvite invite,
        OrganizationDto organization, DateTimeOffset performedAt) =>
        new(invite, organization, performedAt);
}
