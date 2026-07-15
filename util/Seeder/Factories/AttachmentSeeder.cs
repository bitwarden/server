using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Enums;

namespace Bit.Seeder.Factories;

/// <summary>
/// Encrypts a single attachment (via the Rust FFI) and produces the server-side metadata plus the
/// EncArrayBuffer blob to be written to attachment storage.
/// </summary>
internal static class AttachmentSeeder
{
    internal static (string Id, CipherAttachment.MetaData Meta, byte[] Blob) Create(
        byte[] fileBytes,
        string vaultKey,
        string? wrappedCipherKey,
        string fileName,
        AttachmentSchemeType version)
    {
        var encrypted = RustSdkService.EncryptAttachment(fileBytes, vaultKey, wrappedCipherKey, fileName, (uint)version);

        // Matches the production attachment id (see CipherService.CreateAttachmentAsync).
        var id = CoreHelpers.SecureRandomString(32, upper: false, special: false);

        var meta = new CipherAttachment.MetaData
        {
            // Set explicitly: AddAttachment does not populate it, and the storage service uses it for the blob path.
            AttachmentId = id,
            FileName = encrypted.FileName,
            Key = encrypted.Key,
            Size = encrypted.Size,
        };

        return (id, meta, encrypted.Data);
    }
}
