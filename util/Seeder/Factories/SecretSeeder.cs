using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

internal static class SecretSeeder
{
    internal static Secret Create(
        Guid organizationId,
        string orgKey,
        string key,
        string? value,
        string? note,
        IEnumerable<Guid>? projectIds)
    {
        return new Secret
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            Key = RustSdkService.EncryptString(key, orgKey),
            Value = RustSdkService.EncryptString(value ?? string.Empty, orgKey),
            Note = RustSdkService.EncryptString(note ?? string.Empty, orgKey),
            Projects = projectIds?
                .Select(id => new Project { Id = id, OrganizationId = organizationId })
                .ToList()
        };
    }
}
