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
        InvitedUsers =
        [
            .. usersValidationRequest.Invites
                .Select(x => OrganizationUser.CreateInvited(
                    usersValidationRequest.InviteOrganization.OrganizationId, x.Email, x.Type, x.ExternalId))
        ];
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
}
