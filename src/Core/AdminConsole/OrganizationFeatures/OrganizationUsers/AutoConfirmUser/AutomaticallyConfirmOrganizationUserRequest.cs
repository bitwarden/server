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
/// <see cref="Organization"/> objects directly. <see cref="OrganizationUserId"/> and
/// <see cref="OrganizationId"/> mirror the IDs from those objects (set explicitly to support
/// test scenarios where the hydrated objects may be null).
/// </summary>
public record AutomaticallyConfirmOrganizationUserValidationRequest
{
    public required string Key { get; init; }
    public required string DefaultUserCollectionName { get; init; }
    public OrganizationUser? OrganizationUser { get; init; }
    public Organization? Organization { get; init; }
    public IActingUser? PerformedBy { get; init; }
    public Guid OrganizationUserId { get; init; }
    public Guid OrganizationId { get; init; }
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
/// Shared fields (organization, collection name) are specified once rather than
/// repeated on every per-user entry.
/// </summary>
public record BulkAutomaticallyConfirmOrganizationUsersRequest
{
    public required Organization Organization { get; init; }
    public Guid OrganizationId => Organization.Id;
    public required string DefaultUserCollectionName { get; init; }
    public required IReadOnlyList<BulkAutoConfirmUserEntry> UsersToConfirm { get; init; }
}
