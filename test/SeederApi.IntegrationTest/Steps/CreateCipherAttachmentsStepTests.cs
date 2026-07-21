using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Services;
using Bit.RustSDK;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Steps;

public sealed class CreateCipherAttachmentsStepTests : IDisposable
{
    private const string Fixture = "encryption-modes";
    private const string V0Name = "v0 — legacy attachment (no attachment key)";
    private const string V1Name = "v1 — attachment key wrapped by user key";
    private const string V2Name = "v2 — attachment key wrapped by cipher key";
    private const string MultiName = "Multi — v0 + v1 attachments on one cipher";
    private const string Control1Name = "Control — user-key cipher, no attachment";
    private const string Control2Name = "Control — cipher-key cipher, no attachment";

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "seeder-attach-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Execute_SeedsAttachmentsAcrossAllVersions()
    {
        var context = BuildContext(out var baseDir);
        var userKey = RustSdkService.GenerateUserKeys("attach-test@example.com", "asdfasdfasdf").Key;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, userKey));

        CreateCiphersStep.ForPersonalVault(Fixture).Execute(context);
        CreateCipherAttachmentsStep.ForPersonalVault(Fixture).Execute(context);

        // v0 — one attachment with no attachment key; blob decryptable with the vault key.
        var v0Cipher = ByName(context, V0Name);
        var v0 = Assert.Single(v0Cipher.GetAttachments()!);
        Assert.Null(v0.Value.Key);
        AssertBlob(baseDir, v0Cipher.Id, v0.Key, v0.Value.Size);

        // v1 — one attachment whose key is wrapped (user key); blob present.
        var v1Cipher = ByName(context, V1Name);
        var v1 = Assert.Single(v1Cipher.GetAttachments()!);
        Assert.StartsWith("2.", v1.Value.Key);
        AssertBlob(baseDir, v1Cipher.Id, v1.Key, v1.Value.Size);

        // v2 — requires the host cipher to be a cipher-key cipher (Cipher.Key populated).
        var v2Cipher = ByName(context, V2Name);
        Assert.False(string.IsNullOrEmpty(v2Cipher.Key));
        var v2 = Assert.Single(v2Cipher.GetAttachments()!);
        Assert.StartsWith("2.", v2.Value.Key);
        AssertBlob(baseDir, v2Cipher.Id, v2.Key, v2.Value.Size);

        // Multi — two attachments on a single cipher.
        var multi = ByName(context, MultiName).GetAttachments()!;
        Assert.Equal(2, multi.Count);

        // Controls — no attachments.
        Assert.Null(ByName(context, Control1Name).GetAttachments());
        Assert.Null(ByName(context, Control2Name).GetAttachments());
    }

    [Fact]
    public void Execute_V1AttachmentOnCipherKeyCipher_Throws()
    {
        // v1 wraps the attachment key with the vault key, but a client unwraps it with the cipher
        // key when the host cipher has one — so v1 on a cipher-key cipher would be undecryptable.
        var seedFile = new SeedFile
        {
            Items =
            [
                new SeedVaultItem
                {
                    Type = "login",
                    Name = "BadCombo",
                    CipherEncryption = "cipherKey",
                    Login = new SeedLogin { Username = "u@example.com" },
                    Attachments = [new SeedAttachment { File = "mock-seeder-data-recovery-codes-1.txt", AttachmentVersion = "v1" }]
                }
            ]
        };

        var context = BuildContext(new SeederStepTestHelpers.StubSeedReader().Add("ciphers.bad", seedFile), out _);

        // A cipher-key cipher has Cipher.Key populated.
        var cipher = new Cipher { Id = Guid.NewGuid(), Key = "2.fake|fake|fake" };
        context.Ciphers.Add(cipher);
        context.Registry.FixtureCipherNameToId["BadCombo"] = cipher.Id;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, "unused-vault-key"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateCipherAttachmentsStep.ForPersonalVault("bad").Execute(context));
        Assert.Contains("v1", ex.Message);
    }

    [Fact]
    public void Execute_V0AttachmentOnCipherKeyCipher_Throws()
    {
        // v0 encrypts the blob and filename with the vault key, but a client decrypts attachment
        // fields with the cipher key when the host cipher has one — so v0 on a cipher-key cipher
        // would be undecryptable.
        var seedFile = new SeedFile
        {
            Items =
            [
                new SeedVaultItem
                {
                    Type = "login",
                    Name = "BadCombo",
                    CipherEncryption = "cipherKey",
                    Login = new SeedLogin { Username = "u@example.com" },
                    Attachments = [new SeedAttachment { File = "mock-seeder-data-recovery-codes-1.txt", AttachmentVersion = "v0" }]
                }
            ]
        };

        var context = BuildContext(new SeederStepTestHelpers.StubSeedReader().Add("ciphers.bad", seedFile), out _);

        // A cipher-key cipher has Cipher.Key populated.
        var cipher = new Cipher { Id = Guid.NewGuid(), Key = "2.fake|fake|fake" };
        context.Ciphers.Add(cipher);
        context.Registry.FixtureCipherNameToId["BadCombo"] = cipher.Id;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, "unused-vault-key"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateCipherAttachmentsStep.ForPersonalVault("bad").Execute(context));
        Assert.Contains("v0", ex.Message);
    }

    [Fact]
    public void SeedReader_ReadBytes_LoadsBundledSamples()
    {
        var reader = new SeedReader();

        Assert.NotEmpty(reader.ReadBytes("mock-seeder-data-recovery-codes-1.txt"));

        var pdf = reader.ReadBytes("mock-seeder-data-bank-statement-1.pdf");
        Assert.Equal((byte)'%', pdf[0]); // %PDF header
    }

    [Fact]
    public void Execute_NoopStorageWithAttachments_Throws()
    {
        // A fixture that declares attachments but resolves to Noop storage would commit attachment
        // metadata with no blob written — the step must fail fast instead of silently succeeding.
        var context = BuildNoopContext(new SeedReader());
        var userKey = RustSdkService.GenerateUserKeys("noop-test@example.com", "asdfasdfasdf").Key;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, userKey));

        CreateCiphersStep.ForPersonalVault(Fixture).Execute(context);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateCipherAttachmentsStep.ForPersonalVault(Fixture).Execute(context));
        Assert.Contains("NoopAttachmentStorageService", ex.Message);
    }

    [Fact]
    public void Execute_NoopStorageNoAttachments_DoesNotThrow()
    {
        // The guard only fires when the fixture actually declares attachments; a fixture with none
        // must still complete under Noop storage (the early return runs before the guard).
        var seedFile = new SeedFile
        {
            Items =
            [
                new SeedVaultItem
                {
                    Type = "login",
                    Name = "NoAttachments",
                    Login = new SeedLogin { Username = "u@example.com" }
                }
            ]
        };
        var context = BuildNoopContext(new SeederStepTestHelpers.StubSeedReader().Add("ciphers.none", seedFile));
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, "unused-vault-key"));

        CreateCipherAttachmentsStep.ForPersonalVault("none").Execute(context); // must not throw
    }

    private static Cipher ByName(SeederContext context, string name) =>
        context.Ciphers.Single(c => c.Id == context.Registry.FixtureCipherNameToId[name]);

    private static void AssertBlob(string baseDir, Guid cipherId, string attachmentId, long expectedSize)
    {
        var path = Path.Combine(baseDir, cipherId.ToString(), attachmentId);
        Assert.True(File.Exists(path), $"expected attachment blob at {path}");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal((byte)2, bytes[0]);           // AES-256-CBC-HMAC EncArrayBuffer type byte
        Assert.Equal(expectedSize, bytes.Length);  // Size equals the encrypted blob length
    }

    private static SeederContext BuildNoopContext(ISeedReader reader)
    {
        var services = new ServiceCollection();
        services.AddSingleton(reader);
        services.AddSingleton<IAttachmentStorageService>(new NoopAttachmentStorageService());
        return new SeederContext(services.BuildServiceProvider());
    }

    private SeederContext BuildContext(out string baseDir) => BuildContext(new SeedReader(), out baseDir);

    private SeederContext BuildContext(ISeedReader reader, out string baseDir)
    {
        Directory.CreateDirectory(_tempDir);

        var globalSettings = new GlobalSettings();
        globalSettings.Attachment.BaseDirectory = _tempDir;
        globalSettings.BaseServiceUri.Api = "http://localhost";
        baseDir = globalSettings.Attachment.BaseDirectory;

        var storage = new LocalAttachmentStorageService(globalSettings, DataProtectionProvider.Create("seeder-tests"));

        var services = new ServiceCollection();
        services.AddSingleton(reader);
        services.AddSingleton<IAttachmentStorageService>(storage);
        return new SeederContext(services.BuildServiceProvider());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
