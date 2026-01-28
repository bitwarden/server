using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

/// <summary>
/// Factory for creating Folder entities with encrypted names.
/// Folders are per-user constructs encrypted with the user's symmetric key.
/// </summary>
internal sealed class FolderSeeder(RustSdkService sdkService)
{
    /// <summary>
    /// Creates a folder with an encrypted name.
    /// </summary>
    /// <param name="userId">The user who owns this folder.</param>
    /// <param name="userKeyBase64">The user's symmetric key (not org key).</param>
    /// <param name="name">The plaintext folder name to encrypt.</param>
    public Folder CreateFolder(Guid userId, string userKeyBase64, string name)
    {
        return new Folder
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            Name = sdkService.EncryptString(name, userKeyBase64)
        };
    }
}
