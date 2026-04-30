namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

/// <summary>
/// Carries the data needed to authorize changes to a user's group assignments.
/// </summary>
public record OrganizationUserGroupAssignmentResource(
    Guid OrganizationId,
    Guid ActingUserId,
    Guid TargetOrganizationUserId,
    IEnumerable<Guid> PostedGroupIds,
    IEnumerable<Guid> CurrentGroupIds);
