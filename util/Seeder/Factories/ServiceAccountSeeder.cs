using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

internal static class ServiceAccountSeeder
{
    internal static ServiceAccount Create(Guid organizationId, string orgKey, string name)
    {
        return new ServiceAccount
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            Name = RustSdkService.EncryptString(name, orgKey)
        };
    }
}
