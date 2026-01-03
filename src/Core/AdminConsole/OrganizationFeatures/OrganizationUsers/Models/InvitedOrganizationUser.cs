using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Models;

/// <summary>
/// Represents an invitation to join an organization.
/// At this stage, the invitation is sent to an email address but is not yet linked to a specific User account.
/// </summary>
public class InvitedOrganizationUser : IExternal, IOrganizationUserPermissions
{
    /// <summary>
    /// A unique identifier for the organization user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the Organization that the user is invited to join.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The email address of the user invited to the organization.
    /// This is the primary identifier at this stage since the invitation is not yet linked to a User account.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// The role that the user will have in the Organization once they accept and are confirmed.
    /// </summary>
    public OrganizationUserType Type { get; set; }

    /// <summary>
    /// An ID used to identify the OrganizationUser with an external directory service. Used by Directory Connector
    /// and SCIM.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// The date the invitation was created and sent.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// The last date the invitation entry was updated.
    /// </summary>
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public string? Permissions { get; set; }

    /// <summary>
    /// True if the invited user will have access to Secrets Manager for this Organization once confirmed, false otherwise.
    /// </summary>
    public bool AccessSecretsManager { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
