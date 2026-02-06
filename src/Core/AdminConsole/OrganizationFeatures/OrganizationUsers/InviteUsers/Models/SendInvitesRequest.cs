#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

/// <summary>
/// Represents a request to send invitations to a group of organization users.
/// </summary>
public class SendInvitesRequest
{
    public SendInvitesRequest(IEnumerable<OrganizationUser> users, Organization organization) =>
        (Users, Organization) = (users.ToArray(), organization);

    public SendInvitesRequest(IEnumerable<OrganizationUser> users, Organization organization, bool initOrganization) =>
        (Users, Organization, InitOrganization) = (users.ToArray(), organization, initOrganization);

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
}
