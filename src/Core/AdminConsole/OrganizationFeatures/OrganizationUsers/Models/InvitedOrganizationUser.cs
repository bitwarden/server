using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
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

    /// <summary>
    /// Transitions this invited user to an accepted state when the user accepts the invitation.
    /// </summary>
    /// <param name="userId">The ID of the User who accepted the invitation.</param>
    /// <returns>A new <see cref="AcceptedOrganizationUser"/> instance.</returns>
    public AcceptedOrganizationUser ToAccepted(Guid userId)
    {
        return new AcceptedOrganizationUser
        {
            Id = Id,
            OrganizationId = OrganizationId,
            UserId = userId,
            Type = Type,
            ExternalId = ExternalId,
            CreationDate = CreationDate,
            RevisionDate = DateTime.UtcNow,
            Permissions = Permissions,
            AccessSecretsManager = AccessSecretsManager
        };
    }

    /// <summary>
    /// Converts this model to an <see cref="OrganizationUser"/> entity.
    /// </summary>
    /// <returns>An <see cref="OrganizationUser"/> entity with Status set to Invited.</returns>
    public OrganizationUser ToEntity()
    {
        return new OrganizationUser
        {
            Id = Id,
            OrganizationId = OrganizationId,
            UserId = null,
            Email = Email,
            Key = null,
            ResetPasswordKey = null,
            Status = OrganizationUserStatusType.Invited,
            Type = Type,
            ExternalId = ExternalId,
            CreationDate = CreationDate,
            RevisionDate = RevisionDate,
            Permissions = Permissions,
            AccessSecretsManager = AccessSecretsManager
        };
    }

    /// <summary>
    /// Creates an <see cref="InvitedOrganizationUser"/> from an <see cref="OrganizationUser"/> entity.
    /// </summary>
    /// <param name="entity">The entity to convert from. Must have Status = Invited and Email must not be null.</param>
    /// <returns>A new <see cref="InvitedOrganizationUser"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity is not in Invited status or Email is null.</exception>
    public static InvitedOrganizationUser FromEntity(OrganizationUser entity)
    {
        if (entity.Status != OrganizationUserStatusType.Invited)
        {
            throw new InvalidOperationException($"Cannot create InvitedOrganizationUser from entity with status {entity.Status}");
        }

        if (string.IsNullOrEmpty(entity.Email))
        {
            throw new InvalidOperationException("Cannot create InvitedOrganizationUser from entity with null Email");
        }

        return new InvitedOrganizationUser
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            Email = entity.Email,
            Type = entity.Type,
            ExternalId = entity.ExternalId,
            CreationDate = entity.CreationDate,
            RevisionDate = entity.RevisionDate,
            Permissions = entity.Permissions,
            AccessSecretsManager = entity.AccessSecretsManager
        };
    }
}
