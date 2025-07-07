// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersResponse(Guid organizationId)
{
    public IEnumerable<OrganizationUser> InvitedUsers { get; } = [];
    public Guid OrganizationId { get; } = organizationId;

    public InviteOrganizationUsersResponse(InviteOrganizationUsersValidationRequest usersValidationRequest)
        : this(usersValidationRequest.InviteOrganization.OrganizationId)
    {
        InvitedUsers = usersValidationRequest.Invites.Select(x => new OrganizationUser { Email = x.Email });
    }

    public InviteOrganizationUsersResponse(IEnumerable<OrganizationUser> invitedOrganizationUsers, Guid organizationId)
        : this(organizationId)
    {
        InvitedUsers = invitedOrganizationUsers;
    }
}

public class ScimInviteOrganizationUsersResponse
{
    public OrganizationUser InvitedUser { get; init; }

    public ScimInviteOrganizationUsersResponse()
    {

    }

    public ScimInviteOrganizationUsersResponse(InviteOrganizationUsersRequest request)
    {
        var userToInvite = request.Invites.First();

        InvitedUser = new OrganizationUser
        {
            Email = userToInvite.Email,
            ExternalId = userToInvite.ExternalId
        };
    }
}
