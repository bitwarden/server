using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

/// <summary>
/// An association table between one <see cref="User"/> and one <see cref="Organization"/>, representing that user's
/// membership in the organization. "Member" refers to the OrganizationUser object.
/// </summary>
public class OrganizationUser : ITableObject<Guid>, IExternal, IOrganizationUser
{
    /// <summary>
    /// A unique random identifier.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// The ID of the Organization that the user is a member of.
    /// </summary>
    public Guid OrganizationId { get; set; }
    /// <summary>
    /// The ID of the User that is the member. This is NULL if the Status is Invited (or Invited and then Revoked), because
    /// it is not linked to a specific User yet.
    /// </summary>
    public Guid? UserId { get; set; }
    /// <summary>
    /// The email address of the user invited to the organization. This is NULL if the Status is not Invited (or
    /// Invited and then Revoked), because in that case the OrganizationUser is linked to a User
    /// and the email is stored on the User object.
    /// </summary>
    [MaxLength(256)]
    public string? Email { get; set; }
    /// <summary>
    /// The Organization symmetric key encrypted with the User's public key. NULL if the user is not in a Confirmed
    /// (or Confirmed and then Revoked) status.
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// The User's symmetric key encrypted with the Organization's public key. NULL if the OrganizationUser
    /// is not enrolled in account recovery.
    /// </summary>
    public string? ResetPasswordKey { get; set; }
    /// <inheritdoc cref="OrganizationUserStatusType"/>
    public OrganizationUserStatusType Status { get; set; }
    /// <summary>
    /// The User's role in the Organization.
    /// </summary>
    public OrganizationUserType Type { get; set; }
    /// <summary>
    /// An ID used to identify the OrganizationUser with an external directory service. Used by Directory Connector
    /// and SCIM.
    /// </summary>
    [MaxLength(300)]
    public string? ExternalId { get; set; }
    /// <summary>
    /// The date the OrganizationUser was created, i.e. when the User was first invited to the Organization.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    /// <summary>
    /// The last date the OrganizationUser entry was updated.
    /// </summary>
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
    /// <summary>
    /// A json blob representing the <see cref="Bit.Core.Models.Data.Permissions"/> of the OrganizationUser if they
    /// are a Custom user role (i.e. the <see cref="OrganizationUserType"/> is Custom). MAY be NULL if they are not
    /// a custom user, but this is not guaranteed; do not use this to determine their role.
    /// </summary>
    /// <remarks>
    /// Avoid using this property directly - instead use the <see cref="GetPermissions"/> and <see cref="SetPermissions"/>
    /// helper methods.
    /// </remarks>
    public string? Permissions { get; set; }
    /// <summary>
    /// True if the User has access to Secrets Manager for this Organization, false otherwise.
    /// </summary>
    public bool AccessSecretsManager { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public Permissions? GetPermissions()
    {
        return string.IsNullOrWhiteSpace(Permissions) ? null
            : CoreHelpers.LoadClassFromJsonData<Permissions>(Permissions);
    }

    public void SetPermissions(Permissions permissions)
    {
        Permissions = CoreHelpers.ClassToJsonData(permissions);
    }
}
