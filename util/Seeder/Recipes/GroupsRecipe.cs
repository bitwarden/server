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
    public void AddToOrganization(Guid organizationId, int groups, List<Guid> organizationUserIds, int maxUsersWithRelationships = 1000)
    {
        var groupList = new List<Core.AdminConsole.Entities.Group>();
        for (var i = 0; i < groups; i++)
        {
            groupList.Add(new Core.AdminConsole.Entities.Group
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = $"Group {i + 1}"
            });
        }

        if (groupList.Any())
        {
            db.BulkCopy(groupList);

            var maxRelationships = Math.Min(organizationUserIds.Count, maxUsersWithRelationships);
            var groupUsers = new List<Core.AdminConsole.Entities.GroupUser>();

            for (var i = 0; i < maxRelationships; i++)
            {
                var orgUserId = organizationUserIds[i];

                var groupIndex = i % groupList.Count;
                groupUsers.Add(new Core.AdminConsole.Entities.GroupUser
                {
                    GroupId = groupList[groupIndex].Id,
                    OrganizationUserId = orgUserId
                });
            }

            if (groupUsers.Any())
            {
                db.BulkCopy(groupUsers);
            }
        }
    }
}
