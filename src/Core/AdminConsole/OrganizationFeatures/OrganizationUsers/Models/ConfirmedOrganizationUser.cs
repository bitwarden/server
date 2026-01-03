using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Models;

/// <summary>
/// Represents a fully confirmed member of an organization. The user has accepted their invitation and has been
/// confirmed by an organization administrator. At this stage, the user has access to encrypted organization data
/// through the encrypted organization key.
/// </summary>
public class ConfirmedOrganizationUser : ITypedOrganizationUser
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

    /// <summary>
    /// True if the user's access has been revoked, false otherwise.
    /// </summary>
    public bool Revoked { get; set; }

    /// <summary>
    /// Converts this model to an <see cref="OrganizationUser"/> entity.
    /// </summary>
    /// <returns>An <see cref="OrganizationUser"/> entity with Status set to Confirmed or Revoked based on the Revoked flag.</returns>
    public OrganizationUser ToEntity()
    {
        return new OrganizationUser
        {
            Id = Id,
            OrganizationId = OrganizationId,
            UserId = UserId,
            Email = null,
            Key = Key,
            ResetPasswordKey = ResetPasswordKey,
            Status = Revoked ? OrganizationUserStatusType.Revoked : OrganizationUserStatusType.Confirmed,
            Type = Type,
            ExternalId = ExternalId,
            CreationDate = CreationDate,
            RevisionDate = RevisionDate,
            Permissions = Permissions,
            AccessSecretsManager = AccessSecretsManager
        };
    }

    /// <summary>
    /// Creates a <see cref="ConfirmedOrganizationUser"/> from an <see cref="OrganizationUser"/> entity.
    /// </summary>
    /// <param name="entity">The entity to convert from. Must have Status = Confirmed or Revoked (with pre-revoked status of Confirmed), UserId and Key must not be null.</param>
    /// <returns>A new <see cref="ConfirmedOrganizationUser"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity status is invalid, or UserId or Key is null.</exception>
    public static ConfirmedOrganizationUser FromEntity(OrganizationUser entity)
    {
        var isRevoked = entity.Status == OrganizationUserStatusType.Revoked;

        if (!isRevoked && entity.Status != OrganizationUserStatusType.Confirmed)
        {
            throw new InvalidOperationException($"Cannot create ConfirmedOrganizationUser from entity with status {entity.Status}");
        }

        if (isRevoked)
        {
            // Validate that the revoked user's pre-revoked status is Confirmed
            var preRevokedStatus = OrganizationService.GetPriorActiveOrganizationUserStatusType(entity);
            if (preRevokedStatus != OrganizationUserStatusType.Confirmed)
            {
                throw new InvalidOperationException($"Cannot create ConfirmedOrganizationUser from revoked entity with pre-revoked status {preRevokedStatus}");
            }
        }

        if (!entity.UserId.HasValue)
        {
            throw new InvalidOperationException("Cannot create ConfirmedOrganizationUser from entity with null UserId");
        }

        if (string.IsNullOrEmpty(entity.Key))
        {
            throw new InvalidOperationException("Cannot create ConfirmedOrganizationUser from entity with null Key");
        }

        return new ConfirmedOrganizationUser
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            UserId = entity.UserId.Value,
            Key = entity.Key,
            ResetPasswordKey = entity.ResetPasswordKey,
            Type = entity.Type,
            ExternalId = entity.ExternalId,
            CreationDate = entity.CreationDate,
            RevisionDate = entity.RevisionDate,
            Permissions = entity.Permissions,
            AccessSecretsManager = entity.AccessSecretsManager,
            Revoked = isRevoked
        };
    }

    /// <summary>
    /// Implicitly converts a ConfirmedOrganizationUser to an OrganizationUser entity.
    /// </summary>
    public static implicit operator OrganizationUser(ConfirmedOrganizationUser confirmed)
    {
        return confirmed.ToEntity();
    }
}
