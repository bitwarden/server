using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

internal static class ProjectSeeder
{
    internal static Project Create(Guid organizationId, string orgKey, string name)
    {
        return new Project
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            Name = RustSdkService.EncryptString(name, orgKey)
        };
    }
}
