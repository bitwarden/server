using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public record InviteOrganizationUserRequest
{
    public OrganizationUserSingleEmailInvite Invite { get; }
    public OrganizationDto Organization { get; }
    public Guid PerformedBy { get; }
    public DateTimeOffset PerformedAt { get; }

    private InviteOrganizationUserRequest(OrganizationUserSingleEmailInvite Invite,
        OrganizationDto Organization,
        Guid PerformedBy,
        DateTimeOffset PerformedAt)
    {
        this.Invite = Invite;
        this.Organization = Organization;
        this.PerformedBy = PerformedBy;
        this.PerformedAt = PerformedAt;
    }

    public static InviteOrganizationUserRequest Create(OrganizationUserSingleEmailInvite invite,
        OrganizationDto organization, Guid performedBy, DateTimeOffset performedAt) =>
        new(invite, organization, performedBy, performedAt);
}
