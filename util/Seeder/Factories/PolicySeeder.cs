using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class PolicySeeder
{
    internal static Policy Create(Guid organizationId, PolicyType type, bool enabled, string? data = null) => new()
    {
        Id = CombGuid.Generate(),
        OrganizationId = organizationId,
        Type = type,
        Enabled = enabled,
        Data = data
    };
}
