using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

/// <summary>
/// Represents common properties shared by all typed organization user models.
/// </summary>
public interface ITypedOrganizationUser : IExternal, IOrganizationUserPermissions
{
    /// <summary>
    /// A unique identifier for the organization user.
    /// </summary>
    Guid Id { get; set; }

    /// <summary>
    /// The ID of the Organization.
    /// </summary>
    Guid OrganizationId { get; set; }

    /// <summary>
    /// The User's role in the Organization.
    /// </summary>
    OrganizationUserType Type { get; set; }

    /// <summary>
    /// The date the OrganizationUser was created.
    /// </summary>
    DateTime CreationDate { get; }

    /// <summary>
    /// The last date the OrganizationUser entry was updated.
    /// </summary>
    DateTime RevisionDate { get; }

    /// <summary>
    /// True if the User has access to Secrets Manager for this Organization, false otherwise.
    /// </summary>
    bool AccessSecretsManager { get; set; }

    /// <summary>
    /// True if the user's access has been revoked, false otherwise.
    /// </summary>
    bool Revoked { get; set; }

    public OrganizationUser ToEntity();
}
