using Bit.Core.AdminConsole.Entities;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

internal sealed class CreateGroupsStep(int count) : IStep
{
    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var hardenedOrgUserIds = context.Registry.HardenedOrgUserIds;

        var groups = new List<Group>(count);
        var groupIds = new List<Guid>(count);
        var groupUsers = new List<GroupUser>();

        for (var i = 0; i < count; i++)
        {
            var group = GroupSeeder.Create(orgId, $"Group {i + 1}");
            groups.Add(group);
            groupIds.Add(group.Id);
        }

        // Round-robin user assignment
        if (groups.Count > 0 && hardenedOrgUserIds.Count > 0)
        {
            for (var i = 0; i < hardenedOrgUserIds.Count; i++)
            {
                var groupId = groupIds[i % groups.Count];
                groupUsers.Add(GroupUserSeeder.Create(groupId, hardenedOrgUserIds[i]));
            }
        }

        context.Groups.AddRange(groups);
        context.Registry.GroupIds.AddRange(groupIds);
        context.GroupUsers.AddRange(groupUsers);
    }
}
