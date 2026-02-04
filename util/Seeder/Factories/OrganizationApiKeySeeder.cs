using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

public static class OrganizationApiKeySeeder
{
    public static OrganizationApiKey CreateApiKey(Guid organizationId, OrganizationApiKeyType type = OrganizationApiKeyType.Default)
    {
        return new OrganizationApiKey
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            ApiKey = Guid.NewGuid().ToString("N")[..30],
            Type = type,
            RevisionDate = DateTime.UtcNow
        };
    }
}
