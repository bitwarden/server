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
        var groupList = Enumerable.Range(0, groups)
            .Select(i => new Core.AdminConsole.Entities.Group
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = organizationId,
                Name = $"Group {i + 1}"
            })
            .ToList();

        db.BulkCopy(groupList);

        if (groupList.Count > 0 && organizationUserIds.Count > 0 && maxUsersWithRelationships > 0)
        {
            var groupUsers = organizationUserIds
                .Take(maxUsersWithRelationships)
                .Select((orgUserId, i) => new Core.AdminConsole.Entities.GroupUser
                {
                    GroupId = groupList[i % groupList.Count].Id,
                    OrganizationUserId = orgUserId
                })
                .ToList();
            db.BulkCopy(groupUsers);
        }

        return groupList.Select(g => g.Id).ToList();
    }
}
