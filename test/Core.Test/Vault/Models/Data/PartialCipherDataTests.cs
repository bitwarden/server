using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Xunit;

namespace Bit.Core.Test.Vault.Models.Data;

public class PartialCipherDataTests
{
    private const string EncryptedName = "2.name|encrypted";
    private const string SecretUsername = "2.SENTINEL-username|encrypted";
    private const string SecretPassword = "2.SENTINEL-password|encrypted";
    private const string SecretTotp = "2.SENTINEL-totp|encrypted";

    [Fact]
    public void Strip_Login_KeepsNameAndUrisAndDropsSecrets()
    {
        var loginData = new CipherLoginData
        {
            Name = EncryptedName,
            Notes = "2.SENTINEL-notes|encrypted",
            Username = SecretUsername,
            Password = SecretPassword,
            PasswordRevisionDate = DateTime.UtcNow,
            Totp = SecretTotp,
            AutofillOnPageLoad = true,
            Uris = new[]
            {
                new CipherLoginData.CipherLoginUriData { Uri = "2.uri1|encrypted", UriChecksum = "2.checksum1|encrypted", Match = UriMatchType.Domain },
                new CipherLoginData.CipherLoginUriData { Uri = "2.uri2|encrypted" },
            },
            Fido2Credentials = new[] { new CipherLoginFido2CredentialData { CredentialId = "2.SENTINEL-fido2|encrypted" } },
            Fields = new[] { new CipherFieldData { Name = "2.SENTINEL-field|encrypted", Value = "2.SENTINEL-fieldvalue|encrypted", Type = FieldType.Text } },
            PasswordHistory = new[] { new CipherPasswordHistoryData { Password = "2.SENTINEL-history|encrypted", LastUsedDate = DateTime.UtcNow } },
        };

        var stripped = PartialCipherData.Strip(CipherType.Login, JsonSerializer.Serialize(loginData));

        var result = JsonSerializer.Deserialize<CipherLoginData>(stripped);
        Assert.Equal(EncryptedName, result.Name);
        Assert.Equal(2, result.Uris.Count());
        Assert.Equal("2.uri1|encrypted", result.Uris.First().Uri);
        Assert.Equal("2.checksum1|encrypted", result.Uris.First().UriChecksum);
        Assert.Equal(UriMatchType.Domain, result.Uris.First().Match);
        Assert.Null(result.Username);
        Assert.Null(result.Password);
        Assert.Null(result.PasswordRevisionDate);
        Assert.Null(result.Totp);
        Assert.Null(result.AutofillOnPageLoad);
        Assert.Null(result.Fido2Credentials);
        Assert.Null(result.Notes);
        Assert.Null(result.Fields);
        Assert.Null(result.PasswordHistory);

        // No secret value may appear anywhere in the serialized envelope.
        Assert.DoesNotContain("SENTINEL", stripped);
    }

    [Theory]
    [InlineData(CipherType.SecureNote)]
    [InlineData(CipherType.Card)]
    [InlineData(CipherType.Identity)]
    [InlineData(CipherType.SSHKey)]
    [InlineData(CipherType.BankAccount)]
    [InlineData(CipherType.DriversLicense)]
    [InlineData(CipherType.Passport)]
    public void Strip_NonLogin_KeepsOnlyName(CipherType type)
    {
        // A superset blob containing fields specific to several types plus base/secret fields.
        var data = JsonSerializer.Serialize(new
        {
            Name = EncryptedName,
            Notes = "2.SENTINEL-notes|encrypted",
            Number = "2.SENTINEL-cardnumber|encrypted",
            Code = "2.SENTINEL-cvv|encrypted",
            LicenseNumber = "2.SENTINEL-license|encrypted",
            PassportNumber = "2.SENTINEL-passport|encrypted",
            PrivateKey = "2.SENTINEL-sshkey|encrypted",
            Fields = new[] { new { Name = "2.SENTINEL-field|encrypted", Value = "2.SENTINEL-fieldvalue|encrypted" } },
        });

        var stripped = PartialCipherData.Strip(type, data);

        var result = JsonSerializer.Deserialize<CipherCardData>(stripped);
        Assert.Equal(EncryptedName, result.Name);
        Assert.Null(result.Notes);
        Assert.Null(result.Number);
        Assert.Null(result.Code);
        Assert.Null(result.Fields);
        Assert.DoesNotContain("SENTINEL", stripped);
    }

    [Theory]
    [InlineData(CipherType.Login)]
    [InlineData(CipherType.Card)]
    public void Strip_NullOrEmptyData_ReturnedUnchanged(CipherType type)
    {
        Assert.Null(PartialCipherData.Strip(type, null));
        Assert.Equal("", PartialCipherData.Strip(type, ""));
        Assert.Equal("   ", PartialCipherData.Strip(type, "   "));
    }

    [Fact]
    public void Strip_NullName_ProducesValidJsonWithoutThrowing()
    {
        var loginData = new CipherLoginData { Username = SecretUsername };

        var stripped = PartialCipherData.Strip(CipherType.Login, JsonSerializer.Serialize(loginData));

        var result = JsonSerializer.Deserialize<CipherLoginData>(stripped);
        Assert.Null(result.Name);
        Assert.DoesNotContain("SENTINEL", stripped);
    }

    [Fact]
    public void Strip_IsIdempotent()
    {
        var loginData = new CipherLoginData
        {
            Name = EncryptedName,
            Username = SecretUsername,
            Uris = new[] { new CipherLoginData.CipherLoginUriData { Uri = "2.uri|encrypted" } },
        };

        var once = PartialCipherData.Strip(CipherType.Login, JsonSerializer.Serialize(loginData));
        var twice = PartialCipherData.Strip(CipherType.Login, once);

        Assert.Equal(once, twice);
    }
}
