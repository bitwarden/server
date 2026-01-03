using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Models;

/// <summary>
/// Represents a fully confirmed member of an organization. The user has accepted their invitation and has been
/// confirmed by an organization administrator. At this stage, the user has access to encrypted organization data
/// through the encrypted organization key.
/// </summary>
public class ConfirmedOrganizationUser : IExternal, IOrganizationUserPermissions
{
    /// <summary>
    /// A unique identifier for the organization user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the Organization that the user is a confirmed member of.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The ID of the User who is the confirmed member.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The Organization symmetric key encrypted with the User's public key.
    /// This grants the user access to the organization's encrypted data.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The User's symmetric key encrypted with the Organization's public key.
    /// NULL if the OrganizationUser is not enrolled in account recovery.
    /// </summary>
    public string? ResetPasswordKey { get; set; }

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
