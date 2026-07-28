using Bit.Core.Vault.Services;
using Bit.Seeder.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates attachments for fixture ciphers that declare an <c>attachments</c> array.
/// For each attachment this encrypts the bundled sample body,
/// sets the attachment metadata on the in-memory <see cref="Core.Vault.Entities.Cipher"/> (persisted by
/// the BulkCommitter via <c>Cipher.Attachments</c>), and writes the encrypted blob to attachment storage.
/// </summary>
/// <remarks>
/// A no-op when the fixture declares no attachments. A v2 (cipher-key-wrapped) attachment requires the host
/// cipher to have been created with <see cref="CipherEncryptionType.CipherKey"/> so <c>Cipher.Key</c> is populated.
/// </remarks>
internal sealed class CreateCipherAttachmentsStep : IStep
{
    private readonly string _fixtureName;
    private readonly bool _personal;

    private CreateCipherAttachmentsStep(string fixtureName, bool personal)
    {
        _fixtureName = fixtureName;
        _personal = personal;
    }

    internal static CreateCipherAttachmentsStep ForOrganization(string fixtureName) =>
        new(fixtureName, personal: false);

    internal static CreateCipherAttachmentsStep ForPersonalVault(string fixtureName) =>
        new(fixtureName, personal: true);

    public void Execute(SeederContext context)
    {
        var seedFile = context.GetSeedReader().Read<SeedFile>($"ciphers.{_fixtureName}");
        var itemsWithAttachments = seedFile.Items.Where(i => i.Attachments is { Count: > 0 }).ToList();
        if (itemsWithAttachments.Count == 0)
        {
            return;
        }

        var vaultKey = _personal
            ? context.Registry.UserDigests[0].SymmetricKey
            : context.RequireOrgKey();

        var reader = context.GetSeedReader();
        var storage = context.GetAttachmentStorageService();
        if (storage is NoopAttachmentStorageService)
        {
            throw new InvalidOperationException(
                "Attachment fixtures require a configured attachment store, but IAttachmentStorageService " +
                "resolved to NoopAttachmentStorageService (no attachment:connectionString or attachment:baseDirectory). " +
                "Attachment metadata would be committed with no blob written. Configure attachment storage before seeding attachments.");
        }
        var cipherLookup = context.Ciphers.ToDictionary(c => c.Id);
        var nameToId = context.Registry.FixtureCipherNameToId;

        var progress = context.GetProgress();
        var total = itemsWithAttachments.Sum(i => i.Attachments!.Count);
        progress?.Report(new PhaseStarted(SeederPhases.CreatingAttachments, total));

        foreach (var item in itemsWithAttachments)
        {
            if (!nameToId.TryGetValue(item.Name, out var cipherId))
            {
                var available = string.Join(", ", nameToId.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Attachment references unknown cipher '{item.Name}'. Available ciphers: {available}");
            }

            var cipher = cipherLookup[cipherId];

            foreach (var attachment in item.Attachments!)
            {
                var version = ParseVersion(item.Name, attachment.AttachmentVersion);

                // A client unwraps the attachment key with the cipher key when the host cipher has one,
                // otherwise with the vault key. The scheme version must match, or the attachment is undecryptable.
                var hasCipherKey = !string.IsNullOrEmpty(cipher.Key);
                string? wrappedCipherKey = null;
                switch (version)
                {
                    case AttachmentSchemeType.V0 when hasCipherKey:
                        throw new InvalidOperationException(
                            $"Cipher '{item.Name}' has a v0 (no attachment key) attachment but uses a cipher key. " +
                            "A client decrypts attachment fields with the cipher key once the cipher has one, so the " +
                            "vault-key-encrypted bytes would fail to decrypt. Use attachmentVersion \"v2\" for cipher-key ciphers, " +
                            "or remove \"cipherEncryption\": \"cipherKey\".");

                    case AttachmentSchemeType.V1 when hasCipherKey:
                        throw new InvalidOperationException(
                            $"Cipher '{item.Name}' has a v1 (vault-key-wrapped) attachment but uses a cipher key. " +
                            "A client would unwrap the attachment key with the cipher key, not the vault key. " +
                            "Use attachmentVersion \"v2\" for cipher-key ciphers, or remove \"cipherEncryption\": \"cipherKey\".");

                    case AttachmentSchemeType.V2 when !hasCipherKey:
                        throw new InvalidOperationException(
                            $"Cipher '{item.Name}' has a v2 (cipher-key-wrapped) attachment but no cipher key. " +
                            "Set \"cipherEncryption\": \"cipherKey\" on the cipher.");

                    case AttachmentSchemeType.V2:
                        wrappedCipherKey = cipher.Key;
                        break;
                }

                var fileBytes = reader.ReadBytes(attachment.File);
                var displayName = attachment.FileName ?? attachment.File;

                var (id, meta, blob) = AttachmentSeeder.Create(fileBytes, vaultKey, wrappedCipherKey, displayName, version);

                cipher.AddAttachment(id, meta);

                using var stream = new MemoryStream(blob);
                storage.UploadNewAttachmentAsync(stream, cipher, meta).GetAwaiter().GetResult();

                progress?.Report(new PhaseAdvanced(SeederPhases.CreatingAttachments, 1));
            }
        }

        progress?.Report(new PhaseCompleted(SeederPhases.CreatingAttachments));
    }

    private static AttachmentSchemeType ParseVersion(string cipherName, string value) => value switch
    {
        "v0" => AttachmentSchemeType.V0,
        "v1" => AttachmentSchemeType.V1,
        "v2" => AttachmentSchemeType.V2,
        _ => throw new InvalidOperationException(
            $"Cipher '{cipherName}' declares invalid attachmentVersion '{value}'. Expected \"v0\", \"v1\", or \"v2\".")
    };
}
