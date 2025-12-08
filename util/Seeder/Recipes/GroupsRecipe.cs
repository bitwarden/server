using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

public class GroupsRecipe(DatabaseContext db)
{
    /// <summary>
    /// Adds groups to an organization and creates relationships between users and groups.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to add groups to.</param>
    /// <param name="groups">The number of groups to add.</param>
    /// <param name="organizationUserIds">The IDs of the users to create relationships with.</param>
    /// <param name="maxUsersWithRelationships">The maximum number of users to create relationships with.</param>
    public List<Guid> AddToOrganization(Guid organizationId, int groups, List<Guid> organizationUserIds, int maxUsersWithRelationships = 1000)
    {
        var groupList = CreateAndSaveGroups(organizationId, groups);

        if (groupList.Any())
        {
            CreateAndSaveGroupUserRelationships(groupList, organizationUserIds, maxUsersWithRelationships);
        }

        return groupList.Select(g => g.Id).ToList();
    }

    private List<Core.AdminConsole.Entities.Group> CreateAndSaveGroups(Guid organizationId, int count)
    {
        var groupList = new List<Core.AdminConsole.Entities.Group>();

        for (var i = 0; i < count; i++)
        {
            groupList.Add(new Core.AdminConsole.Entities.Group
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = organizationId,
                Name = $"Group {i + 1}"
            });
        }

        if (groupList.Any())
        {
            db.BulkCopy(groupList);
        }

        return groupList;
    }

    private void CreateAndSaveGroupUserRelationships(
        List<Core.AdminConsole.Entities.Group> groups,
        List<Guid> organizationUserIds,
        int maxUsersWithRelationships)
    {
        if (!organizationUserIds.Any() || maxUsersWithRelationships <= 0)
        {
            return;
        }

        var groupUsers = BuildGroupUserRelationships(groups, organizationUserIds, maxUsersWithRelationships);

        if (groupUsers.Any())
        {
            db.BulkCopy(groupUsers);
        }
    }

    /// <summary>
    /// Creates user-to-group relationships with distributed assignment patterns for realistic test data.
    /// Each user is assigned to one group, distributed evenly across available groups.
    /// </summary>
    private List<Core.AdminConsole.Entities.GroupUser> BuildGroupUserRelationships(
        List<Core.AdminConsole.Entities.Group> groups,
        List<Guid> organizationUserIds,
        int maxUsersWithRelationships)
    {
        var maxRelationships = Math.Min(organizationUserIds.Count, maxUsersWithRelationships);
        var groupUsers = new List<Core.AdminConsole.Entities.GroupUser>();

        for (var i = 0; i < maxRelationships; i++)
        {
            var orgUserId = organizationUserIds[i];
            var groupIndex = i % groups.Count; // Round-robin distribution across groups

            groupUsers.Add(new Core.AdminConsole.Entities.GroupUser
            {
                GroupId = groups[groupIndex].Id,
                OrganizationUserId = orgUserId
            });
        }

        return groupUsers;
    }
}
