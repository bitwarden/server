using Bit.Core.AdminConsole.Entities;

namespace Bit.Seeder.Factories;

internal static class GroupUserSeeder
{
    internal static GroupUser Create(Guid groupId, Guid organizationUserId)
    {
        return new GroupUser
        {
            GroupId = groupId,
            OrganizationUserId = organizationUserId
        };
    }
}
