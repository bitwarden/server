using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public interface IScopedApiKeyGroupMemberValidator
{
    /// <summary>
    /// Checks that a scoped organization API key only adds members with the User role to a group, or removes them
    /// from it. Members of other organizations are ignored. Always valid for other callers.
    /// </summary>
    /// <param name="organizationId">The organization the group belongs to.</param>
    /// <param name="groupId">The group being changed, or <c>null</c> for a new group with no members yet.</param>
    /// <param name="memberIds">The organization user ids the group will contain after the change.</param>
    /// <returns><c>null</c> when valid, otherwise the <see cref="Error"/> describing why it is not.</returns>
    Task<Error?> ValidateAsync(Guid organizationId, Guid? groupId, IEnumerable<Guid> memberIds);
}
