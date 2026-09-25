using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Xunit;

namespace Bit.Core.Test.Vault.Models.Data;

public class PartialCipherDataTests
{
    // Any occurrence of this marker in the stripped output means a field leaked through.
    private const string Sentinel = "2.SENTINEL|encrypted";

    [Fact]
    public void Strip_Login_KeepsNameAndUris()
    {
        var data = JsonSerializer.Serialize(new CipherLoginData
        {
            Name = "2.name|encrypted",
            Uris =
            [
                new CipherLoginData.CipherLoginUriData
                {
                    Uri = "2.uri|encrypted",
                    UriChecksum = "2.checksum|encrypted",
                    Match = UriMatchType.Host,
                },
            ],
        });

        var stripped = PartialCipherData.Strip(CipherType.Login, data);
        var result = JsonSerializer.Deserialize<CipherLoginData>(stripped, JsonHelpers.IgnoreCase);

        Assert.Equal("2.name|encrypted", result.Name);
        Assert.Single(result.Uris);
        Assert.Equal("2.uri|encrypted", result.Uris.First().Uri);
        Assert.Equal("2.checksum|encrypted", result.Uris.First().UriChecksum);
        Assert.Equal(UriMatchType.Host, result.Uris.First().Match);
    }

    [Fact]
    public void Strip_Login_EmitsCamelCaseEnvelope()
    {
        // The stripped output is the SDK's restricted-decrypt contract: a purpose-built camelCase
        // envelope of name + uris only. Assert the wire shape directly — a casing change or the
        // legacy singular `Uri` getter leaking back in would silently break SDK deserialization.
        var data = JsonSerializer.Serialize(new CipherLoginData
        {
            Name = "2.name|encrypted",
            Uris =
            [
                new CipherLoginData.CipherLoginUriData
                {
                    Uri = "2.uri|encrypted",
                    UriChecksum = "2.checksum|encrypted",
                    Match = UriMatchType.Host,
                },
            ],
        });

        var stripped = PartialCipherData.Strip(CipherType.Login, data);

        using var doc = JsonDocument.Parse(stripped);
        var root = doc.RootElement;
        // Top-level allowlist: exactly name + uris — no singular `uri`, no secret fields.
        Assert.Equal(new[] { "name", "uris" }, root.EnumerateObject().Select(p => p.Name).ToArray());

        var uri = root.GetProperty("uris")[0];
        Assert.Equal(
            new[] { "uri", "uriChecksum", "match" },
            uri.EnumerateObject().Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Strip_Login_DropsEverySecretField()
    {
        var data = JsonSerializer.Serialize(new CipherLoginData
        {
            Name = "2.name|encrypted",
            Username = Sentinel,
            Password = Sentinel,
            PasswordRevisionDate = DateTime.UtcNow,
            Totp = Sentinel,
            AutofillOnPageLoad = true,
            Notes = Sentinel,
            Fields = [new CipherFieldData { Name = Sentinel, Value = Sentinel, Type = FieldType.Text }],
            PasswordHistory = [new CipherPasswordHistoryData { Password = Sentinel }],
        });

        var stripped = PartialCipherData.Strip(CipherType.Login, data);

        // Assert on the raw string as well as the deserialized shape: a field that survives under an
        // unexpected key would still be a leak.
        Assert.DoesNotContain("SENTINEL", stripped);

        var result = JsonSerializer.Deserialize<CipherLoginData>(stripped, JsonHelpers.IgnoreCase);
        Assert.Null(result.Username);
        Assert.Null(result.Password);
        Assert.Null(result.PasswordRevisionDate);
        Assert.Null(result.Totp);
        Assert.Null(result.AutofillOnPageLoad);
        Assert.Null(result.Notes);
        Assert.Null(result.Fields);
        Assert.Null(result.PasswordHistory);
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
        // A deliberately over-broad blob: whatever the type, only Name may survive.
        var data = $$"""
            {
              "Name": "2.name|encrypted",
              "Notes": "{{Sentinel}}",
              "Number": "{{Sentinel}}",
              "Code": "{{Sentinel}}",
              "PrivateKey": "{{Sentinel}}",
              "RoutingNumber": "{{Sentinel}}",
              "LicenseNumber": "{{Sentinel}}",
              "PassportNumber": "{{Sentinel}}",
              "Fields": [{ "Name": "{{Sentinel}}", "Value": "{{Sentinel}}", "Type": 0 }]
            }
            """;

        var stripped = PartialCipherData.Strip(type, data);

        Assert.DoesNotContain("SENTINEL", stripped);
        Assert.Contains("2.name|encrypted", stripped);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Strip_NullOrWhitespace_ReturnsInputUnchanged(string? data)
    {
        Assert.Equal(data, PartialCipherData.Strip(CipherType.Login, data));
    }

    [Fact]
    public void Strip_MissingName_DoesNotThrow()
    {
        var stripped = PartialCipherData.Strip(CipherType.SecureNote, """{"Notes":"2.notes|encrypted"}""");

        Assert.DoesNotContain("2.notes|encrypted", stripped);
    }

    [Fact]
    public void Strip_IsIdempotent()
    {
        var data = JsonSerializer.Serialize(new CipherLoginData
        {
            Name = "2.name|encrypted",
            Password = Sentinel,
            Uris = [new CipherLoginData.CipherLoginUriData { Uri = "2.uri|encrypted" }],
        });

        var once = PartialCipherData.Strip(CipherType.Login, data);
        var twice = PartialCipherData.Strip(CipherType.Login, once);

        Assert.Equal(once, twice);
    }
}
