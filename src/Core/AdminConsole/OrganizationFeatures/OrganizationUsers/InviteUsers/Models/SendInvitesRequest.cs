#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

/// <summary>
/// Represents a request to send invitations to a group of organization users.
/// </summary>
public class SendInvitesRequest
{
    public SendInvitesRequest(IEnumerable<OrganizationUser> users, Organization organization, bool initOrganization = false, Guid? invitingUserId = null) =>
        (Users, Organization, InitOrganization, InvitingUserId) = (users.ToArray(), organization, initOrganization, invitingUserId);

    /// <summary>
    /// Organization Users to send emails to.
    /// </summary>
    public OrganizationUser[] Users { get; set; } = [];

    /// <summary>
    /// The organization to invite the users to.
    /// </summary>
    public Organization Organization { get; init; }

    /// <summary>
    /// This is for when the organization is being created and this is the owners initial invite
    /// </summary>
    public bool InitOrganization { get; init; }

    /// <summary>
    /// The user ID of the person sending the invitation (null for SCIM/automated invitations)
    /// </summary>
    public Guid? InvitingUserId { get; init; }
}
