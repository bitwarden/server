using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

internal static class CollectionSeeder
{
    internal static Collection Create(Guid organizationId, string orgKey, string name)
    {
        return new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            Name = RustSdkService.EncryptString(name, orgKey),
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
    }
}
