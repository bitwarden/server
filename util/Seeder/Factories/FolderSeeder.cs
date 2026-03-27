using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

internal static class FolderSeeder
{
    internal static Folder Create(Guid userId, string userKeyBase64, string name)
    {
        return new Folder
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            Name = RustSdkService.EncryptString(name, userKeyBase64)
        };
    }
}
