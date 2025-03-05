using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersRequest
{
    public OrganizationUserInvite[] Invites { get; } = [];
    public OrganizationDto Organization { get; }
    public Guid PerformedBy { get; }
    public DateTimeOffset PerformedAt { get; }

    public InviteOrganizationUsersRequest(OrganizationUserInvite[] invites,
        OrganizationDto organization,
        Guid performedBy,
        DateTimeOffset performedAt)
    {
        Invites = invites;
        Organization = organization;
        PerformedBy = performedBy;
        PerformedAt = performedAt;
    }

    public InviteOrganizationUsersRequest(InviteScimOrganizationUserRequest request) :
        this([OrganizationUserInvite.Create(request, request.ExternalId)],
            request.Organization,
            Guid.Empty,
            request.PerformedAt)
    { }
}
