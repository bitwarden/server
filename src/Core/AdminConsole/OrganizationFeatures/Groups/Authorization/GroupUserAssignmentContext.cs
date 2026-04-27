namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

/// <summary>
/// Context passed to the authorization handler when assigning users to groups.
/// </summary>
/// <param name="OrganizationId">The organization the group belongs to.</param>
/// <param name="RequestedUserIds">The OrganizationUser IDs being requested for group membership.</param>
/// <param name="GroupId">
/// When set, the handler verifies the current user is not being newly added (i.e. already a member is allowed).
/// When null, any self-assignment is blocked regardless of current membership.
/// </param>
public record GroupUserAssignmentContext(
    Guid OrganizationId,
    IEnumerable<Guid> RequestedUserIds,
    Guid? GroupId = null);
