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

    [Fact]
    public void CreateFido2Credential_Generated_CredentialIdIsPlainGuid()
    {
        var credential = LoginCipherSeeder.CreateFido2Credential("example.com", "Example", "user@example.com");

        Assert.True(Guid.TryParseExact(credential.CredentialId, "D", out _), credential.CredentialId);
    }

    [Theory]
    [InlineData("5096ee6c-448c-47a6-bfad-28dd9e14d4c6")]
    [InlineData("5096EE6C-448C-47A6-BFAD-28DD9E14D4C6")]
    [InlineData("b64.AQIDBA")]
    public void CreateFido2Credential_ClientParsableCredentialId_Accepted(string credentialId)
    {
        var credential = LoginCipherSeeder.CreateFido2Credential(
            "example.com", "Example", "user@example.com", credentialId: credentialId);

        Assert.Equal(credentialId, credential.CredentialId);
    }

    [Theory]
    [InlineData("AQIDBA")]
    [InlineData("b64.")]
    [InlineData("b64.!!!")]
    [InlineData("b64.A")]
    [InlineData("\"5096ee6c-448c-47a6-bfad-28dd9e14d4c6\"")]
    [InlineData("5096ee6c448c47a6bfad28dd9e14d4c6")]
    public void CreateFido2Credential_UnparsableCredentialId_Throws(string credentialId)
    {
        Assert.Throws<ArgumentException>(() => LoginCipherSeeder.CreateFido2Credential(
            "example.com", "Example", "user@example.com", credentialId: credentialId));
    }
}
