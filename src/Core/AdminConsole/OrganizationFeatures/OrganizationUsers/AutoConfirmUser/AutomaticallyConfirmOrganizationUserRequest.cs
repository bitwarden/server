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
/// Automatically Confirm User Validation Request
/// </summary>
/// <remarks>
/// This is used to hold retrieved data and pass it to the validator
/// </remarks>
public record AutomaticallyConfirmOrganizationUserValidationRequest : AutomaticallyConfirmOrganizationUserRequest
{
    public OrganizationUser? OrganizationUser { get; set; }
    public Organization? Organization { get; set; }
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
