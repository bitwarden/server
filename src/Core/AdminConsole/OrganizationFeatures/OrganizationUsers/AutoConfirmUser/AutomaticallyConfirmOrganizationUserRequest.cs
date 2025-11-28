using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

/// <summary>
/// Automatically Confirm User Command Request
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
