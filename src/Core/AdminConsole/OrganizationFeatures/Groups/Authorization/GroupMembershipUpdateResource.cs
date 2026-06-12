namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

/// <summary>
/// Carries the data needed to authorize a group's membership changes.
/// </summary>
public record GroupMembershipUpdateResource(
    Guid OrganizationId,
    Guid ActingUserId,
    IEnumerable<Guid> PostedMemberOrganizationUserIds,
    IEnumerable<Guid> CurrentMemberOrganizationUserIds);
