using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

/// <summary>
/// Request to invite staged organization members with the access they were provisioned with.
/// </summary>
/// <remarks>
/// Backs the members-grid row action. Configuring role, collections, groups, or Secrets Manager access goes
/// through the invite dialog instead, which is email-keyed because a staged member cannot be distinguished
/// from a new one by email alone.
/// </remarks>
public record InviteStagedOrganizationUsersRequest
{
    /// <summary>The organization the members belong to.</summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>The staged <see cref="OrganizationUser"/> rows to invite.</summary>
    public required IEnumerable<Guid> OrganizationUserIds { get; init; }

    /// <summary>The administrator sending the invitations; named as the inviter on the invitation email.</summary>
    public required Guid PerformedBy { get; init; }
}
