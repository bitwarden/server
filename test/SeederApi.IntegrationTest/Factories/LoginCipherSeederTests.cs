using System.Text.Json;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class LoginCipherSeederTests
{
    // The SDK's own test vector for https://example.com.
    private const string _exampleDotComChecksum = "EAaArVRs5qV39C9S3zO0z9ynVoWeZkuNfeMpsVDQnOk=";

    private static CipherSeed NewSeed(string key) => new()
    {
        Type = CipherType.Login,
        Name = "Example Login",
        EncryptionKey = key,
        OrganizationId = Guid.NewGuid(),
        Login = new LoginViewDto { Uris = [new LoginUriViewDto { Uri = "https://example.com" }] }
    };

    [Fact]
    public void Create_UserKeyLoginWithUri_ChecksumDecryptsToSha256OfUri()
    {
        var orgKey = RustSdkService.GenerateOrganizationKeys().Key;

        var cipher = LoginCipherSeeder.Create(NewSeed(orgKey));

        var checksum = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data)!.Uris!.Single().UriChecksum;
        Assert.StartsWith("2.", checksum);
        Assert.Equal(_exampleDotComChecksum, RustSdkService.DecryptString(checksum, orgKey));
    }

    [Fact]
    public void Create_CipherKeyLoginWithUri_ChecksumIsEncrypted()
    {
        var orgKey = RustSdkService.GenerateOrganizationKeys().Key;

        var cipher = LoginCipherSeeder.Create(NewSeed(orgKey) with { CipherEncryption = CipherEncryptionType.CipherKey });

        Assert.StartsWith("2.", cipher.Key);
        Assert.StartsWith("2.", JsonSerializer.Deserialize<CipherLoginData>(cipher.Data)!.Uris!.Single().UriChecksum);
    }
}
