using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
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

    /// <summary>
    /// Transitions this accepted user to a confirmed state when an organization admin confirms them.
    /// </summary>
    /// <param name="key">The Organization symmetric key encrypted with the User's public key.</param>
    /// <returns>A new <see cref="ConfirmedOrganizationUser"/> instance.</returns>
    public ConfirmedOrganizationUser ToConfirmed(string key)
    {
        return new ConfirmedOrganizationUser
        {
            Id = Id,
            OrganizationId = OrganizationId,
            UserId = UserId,
            Key = key,
            ResetPasswordKey = null,
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
    /// <returns>An <see cref="OrganizationUser"/> entity with Status set to Accepted.</returns>
    public OrganizationUser ToEntity()
    {
        return new OrganizationUser
        {
            Id = Id,
            OrganizationId = OrganizationId,
            UserId = UserId,
            Email = null,
            Key = null,
            ResetPasswordKey = null,
            Status = OrganizationUserStatusType.Accepted,
            Type = Type,
            ExternalId = ExternalId,
            CreationDate = CreationDate,
            RevisionDate = RevisionDate,
            Permissions = Permissions,
            AccessSecretsManager = AccessSecretsManager
        };
    }

    /// <summary>
    /// Creates an <see cref="AcceptedOrganizationUser"/> from an <see cref="OrganizationUser"/> entity.
    /// </summary>
    /// <param name="entity">The entity to convert from. Must have Status = Accepted and UserId must not be null.</param>
    /// <returns>A new <see cref="AcceptedOrganizationUser"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity is not in Accepted status or UserId is null.</exception>
    public static AcceptedOrganizationUser FromEntity(OrganizationUser entity)
    {
        if (entity.Status != OrganizationUserStatusType.Accepted)
        {
            throw new InvalidOperationException($"Cannot create AcceptedOrganizationUser from entity with status {entity.Status}");
        }

        if (!entity.UserId.HasValue)
        {
            throw new InvalidOperationException("Cannot create AcceptedOrganizationUser from entity with null UserId");
        }

        return new AcceptedOrganizationUser
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            UserId = entity.UserId.Value,
            Type = entity.Type,
            ExternalId = entity.ExternalId,
            CreationDate = entity.CreationDate,
            RevisionDate = entity.RevisionDate,
            Permissions = entity.Permissions,
            AccessSecretsManager = entity.AccessSecretsManager
        };
    }
}
