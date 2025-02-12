using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteScimOrganizationUserRequest
{
    public OrganizationUserSingleEmailInvite Invite { get; }
    public OrganizationDto Organization { get; }
    public DateTimeOffset PerformedAt { get; }
    public string ExternalId { get; } = string.Empty;

    private InviteScimOrganizationUserRequest(OrganizationUserSingleEmailInvite invite,
        OrganizationDto organization,
        DateTimeOffset performedAt,
        string externalId)
    {
        Invite = invite;
        Organization = organization;
        PerformedAt = performedAt;
        ExternalId = externalId;
    }

    public static InviteScimOrganizationUserRequest Create(OrganizationUserSingleEmailInvite invite,
        OrganizationDto organization, DateTimeOffset performedAt, string externalId) =>
        new(invite, organization, performedAt, externalId);
}
