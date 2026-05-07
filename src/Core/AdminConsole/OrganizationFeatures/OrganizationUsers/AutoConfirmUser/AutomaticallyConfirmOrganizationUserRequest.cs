using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

/// <summary>
/// Automatically Confirm User Command Request (single-user).
/// </summary>
public record AutomaticallyConfirmOrganizationUserRequest
{
    public required Guid OrganizationUserId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string Key { get; init; }
    public required string DefaultUserCollectionName { get; init; }
    public required IActingUser PerformedBy { get; init; }
}

/// <summary>
/// Hydrated request passed to the validator. Carries the retrieved <see cref="OrganizationUser"/> and
/// <see cref="Organization"/> objects directly; <see cref="OrganizationUserId"/> and
/// <see cref="OrganizationId"/> are derived from those objects to prevent the two copies from diverging.
/// </summary>
public record AutomaticallyConfirmOrganizationUserValidationRequest
{
    public required string Key { get; init; }
    public required string DefaultUserCollectionName { get; init; }
    public required IActingUser PerformedBy { get; init; }
    public OrganizationUser? OrganizationUser { get; init; }
    public Organization? Organization { get; init; }

    /// <summary>Derived from <see cref="OrganizationUser"/>.</summary>
    public Guid OrganizationUserId => OrganizationUser!.Id;

    /// <summary>Derived from <see cref="Organization"/>.</summary>
    public Guid OrganizationId => Organization!.Id;
}

/// <summary>
/// Per-user entry for a bulk auto-confirm operation, containing only the user-specific fields.
/// </summary>
public record BulkAutoConfirmUserEntry
{
    public required Guid OrganizationUserId { get; init; }
    public required string Key { get; init; }
}

/// <summary>
/// Top-level request for bulk automatic user confirmation.
/// Shared fields (organization, collection name, actor) are specified once rather than
/// repeated on every per-user entry.
/// </summary>
public record BulkAutomaticallyConfirmOrganizationUsersRequest
{
    public required Guid OrganizationId { get; init; }
    public required string DefaultUserCollectionName { get; init; }
    public required IActingUser PerformedBy { get; init; }
    public required IReadOnlyList<BulkAutoConfirmUserEntry> UsersToConfirm { get; init; }
}
