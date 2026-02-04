using Bit.Core.AdminConsole.Entities;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

/// <summary>
/// Creates groups and group-user relationships for seeding.
/// </summary>
public static class GroupSeeder
{
    /// <summary>
    /// Creates a group entity for an organization.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="name">The group name.</param>
    /// <returns>A new Group entity (not persisted).</returns>
    public static Group CreateGroup(Guid organizationId, string name)
    {
        return new Group
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            Name = name
        };
    }

    /// <summary>
    /// Creates a group-user relationship entity.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="organizationUserId">The organization user ID.</param>
    /// <returns>A new GroupUser entity (not persisted).</returns>
    public static GroupUser CreateGroupUser(Guid groupId, Guid organizationUserId)
    {
        return new GroupUser
        {
            GroupId = groupId,
            OrganizationUserId = organizationUserId
        };
    }
}
