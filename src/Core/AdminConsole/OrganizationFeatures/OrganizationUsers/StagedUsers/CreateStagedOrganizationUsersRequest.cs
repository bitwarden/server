using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

/// <summary>
/// A single user to provision in <see cref="OrganizationUserStatusType.Staged"/> status.
/// </summary>
public record StagedOrganizationUserRequest
{
    public required string Email { get; init; }
    public required string ExternalId { get; init; }
}

/// <summary>
/// Request to create staged organization users.
/// </summary>
public record CreateStagedOrganizationUsersRequest
{
    /// <summary>The organization to provision the staged users into.</summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// The users to stage. Emails already present in the organization, and duplicate emails within
    /// the batch, are skipped.
    /// </summary>
    public required IEnumerable<StagedOrganizationUserRequest> Users { get; init; }

    /// <summary>The automated system performing the provisioning, used for event attribution.</summary>
    public required EventSystemUser EventSystemUser { get; init; }
}
