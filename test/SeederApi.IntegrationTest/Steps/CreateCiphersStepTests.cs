using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.RustSDK;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Steps;

public sealed class CreateCiphersStepTests
{
    [Fact]
    public void Execute_PersonalArchivedAndDeletedFlags_StampLifecycleState()
    {
        var seedFile = new SeedFile
        {
            Items =
            [
                Login("Archived"),
                Login("Deleted"),
                Login("Both"),
                Login("Plain")
            ]
        };
        seedFile.Items[0] = seedFile.Items[0] with { Archived = true };
        seedFile.Items[1] = seedFile.Items[1] with { Deleted = true };
        seedFile.Items[2] = seedFile.Items[2] with { Archived = true, Deleted = true };

        var reader = new SeederStepTestHelpers.StubSeedReader().Add("ciphers.lifecycle", seedFile);
        var context = NewContext(reader);
        var userId = Guid.NewGuid();
        var userKey = RustSdkService.GenerateUserKeys("lifecycle@example.com", "asdfasdfasdf").Key;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(userId, Guid.Empty, userKey));

        CreateCiphersStep.ForPersonalVault("lifecycle").Execute(context);

        var archived = ByName(context, "Archived");
        Assert.Contains(userId.ToString().ToUpperInvariant(), archived.Archives);
        Assert.Null(archived.DeletedDate);
        Assert.True(archived.CreationDate < DateTime.UtcNow, "archived CreationDate should be backdated");

        var deleted = ByName(context, "Deleted");
        Assert.NotNull(deleted.DeletedDate);
        Assert.Null(deleted.Archives);
        Assert.True(deleted.CreationDate < DateTime.UtcNow, "deleted CreationDate should be backdated");

        var both = ByName(context, "Both");
        Assert.Contains(userId.ToString().ToUpperInvariant(), both.Archives);
        Assert.NotNull(both.DeletedDate);

        var plain = ByName(context, "Plain");
        Assert.Null(plain.Archives);
        Assert.Null(plain.DeletedDate);
    }

    [Fact]
    public void Execute_OrgArchived_AttributesArchiveToOwner()
    {
        var seedFile = new SeedFile { Items = [Login("OrgArchived") with { Archived = true }] };
        var reader = new SeederStepTestHelpers.StubSeedReader().Add("ciphers.orglifecycle", seedFile);
        var context = NewContext(reader);
        SeederStepTestHelpers.PreloadOrganization(context);

        var ownerId = Guid.NewGuid();
        context.Owner = new User { Id = ownerId };

        CreateCiphersStep.ForOrganization("orglifecycle").Execute(context);

        var cipher = ByName(context, "OrgArchived");
        Assert.Contains(ownerId.ToString().ToUpperInvariant(), cipher.Archives);
    }

    [Theory]
    [InlineData("bankAccount", CipherType.BankAccount)]
    [InlineData("driversLicense", CipherType.DriversLicense)]
    [InlineData("passport", CipherType.Passport)]
    public void Execute_NewCipherType_CipherKey_DispatchesAndWrapsCipherKey(string type, CipherType expected)
    {
        // Proves the dispatch arm exists for the type AND that the factory forwards cipherEncryption:
        // a cipherKey request must populate Cipher.Key (the per-cipher key wrapped by the vault key).
        var item = new SeedVaultItem
        {
            Type = type,
            Name = "NewType",
            CipherEncryption = "cipherKey",
            BankAccount = type == "bankAccount" ? new SeedBankAccount { BankName = "Example Bank", AccountNumber = "000000000" } : null,
            DriversLicense = type == "driversLicense" ? new SeedDriversLicense { LicenseNumber = "D0000000" } : null,
            Passport = type == "passport" ? new SeedPassport { PassportNumber = "X0000000" } : null
        };

        var reader = new SeederStepTestHelpers.StubSeedReader().Add("ciphers.newtype", new SeedFile { Items = [item] });
        var context = NewContext(reader);
        var userKey = RustSdkService.GenerateUserKeys("newtype@example.com", "asdfasdfasdf").Key;
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.Empty, userKey));

        CreateCiphersStep.ForPersonalVault("newtype").Execute(context);

        var cipher = ByName(context, "NewType");
        Assert.Equal(expected, cipher.Type);
        Assert.StartsWith("2.", cipher.Key);
    }

    private static SeedVaultItem Login(string name) => new()
    {
        Type = "login",
        Name = name,
        Login = new SeedLogin { Username = $"{name}@example.com", Password = "fake-example-password" }
    };

    private static Cipher ByName(SeederContext context, string name) =>
        context.Ciphers.Single(c => c.Id == context.Registry.FixtureCipherNameToId[name]);

    private static SeederContext NewContext(ISeedReader reader)
    {
        var services = new ServiceCollection();
        services.AddSingleton(reader);
        services.AddSingleton<IManglerService>(new NoOpManglerService());
        return new SeederContext(services.BuildServiceProvider());
    }
}
