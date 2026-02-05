using Bit.Core.AdminConsole.Entities;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class GroupSeeder
{
    internal static Group Create(Guid organizationId, string name)
    {
        return new Group
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            Name = name
        };
    }
}
