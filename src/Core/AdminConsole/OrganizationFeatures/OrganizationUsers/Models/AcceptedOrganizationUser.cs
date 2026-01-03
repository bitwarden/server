using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Models;

/// <summary>
/// Represents a user who has accepted their invitation to join an organization but has not yet been confirmed
/// by an organization administrator. At this stage, the user is linked to a User account but does not yet have
/// access to encrypted organization data.
/// </summary>
public class AcceptedOrganizationUser : IExternal, IOrganizationUserPermissions
{
    /// <summary>
    /// A unique identifier for the organization user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the Organization that the user has accepted membership to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The ID of the User who accepted the invitation. This is now linked to a specific User account.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The User's role in the Organization.
    /// </summary>
    public OrganizationUserType Type { get; set; }

    /// <summary>
    /// An ID used to identify the OrganizationUser with an external directory service. Used by Directory Connector
    /// and SCIM.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// The date the OrganizationUser was created, i.e. when the User was first invited to the Organization.
    /// </summary>
    public DateTime CreationDate { get; internal set; }

    /// <summary>
    /// The last date the OrganizationUser entry was updated.
    /// </summary>
    public DateTime RevisionDate { get; internal set; }

    /// <inheritdoc />
    public string? Permissions { get; set; }

    /// <summary>
    /// True if the User has access to Secrets Manager for this Organization, false otherwise.
    /// </summary>
    public bool AccessSecretsManager { get; set; }
}
