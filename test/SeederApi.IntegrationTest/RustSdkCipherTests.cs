using System.Text.Json;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class RustSdkCipherTests
{
    private static readonly JsonSerializerOptions SdkJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void EncryptDecrypt_LoginCipher_RoundtripPreservesPlaintext()
    {
        var sdk = new RustSdkService();
        var orgKeys = sdk.GenerateOrganizationKeys();

        var originalCipher = CreateTestLoginCipher();
        var originalJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);

        var encryptedJson = sdk.EncryptCipher(originalJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.Contains("\"name\":\"2.", encryptedJson);

        var decryptedJson = sdk.DecryptCipher(encryptedJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", decryptedJson);

        var decryptedCipher = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);

        Assert.NotNull(decryptedCipher);
        Assert.Equal(originalCipher.Name, decryptedCipher.Name);
        Assert.Equal(originalCipher.Notes, decryptedCipher.Notes);
        Assert.Equal(originalCipher.Login?.Username, decryptedCipher.Login?.Username);
        Assert.Equal(originalCipher.Login?.Password, decryptedCipher.Login?.Password);
    }

    [Fact]
    public void EncryptCipher_WithUri_EncryptsAllFields()
    {
        var sdk = new RustSdkService();
        var orgKeys = sdk.GenerateOrganizationKeys();

        var cipher = new CipherViewDto
        {
            Name = "Amazon Shopping",
            Notes = "Prime member since 2020",
            Type = CipherTypes.Login,
            Login = new LoginViewDto
            {
                Username = "shopper@example.com",
                Password = "MySecretPassword123!",
                Uris =
                [
                    new LoginUriViewDto { Uri = "https://amazon.com/login" },
                    new LoginUriViewDto { Uri = "https://www.amazon.com" }
                ]
            }
        };

        var cipherJson = JsonSerializer.Serialize(cipher, SdkJsonOptions);
        var encryptedJson = sdk.EncryptCipher(cipherJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("Amazon Shopping", encryptedJson);
        Assert.DoesNotContain("shopper@example.com", encryptedJson);
        Assert.DoesNotContain("MySecretPassword123!", encryptedJson);
    }

    [Fact]
    public void DecryptCipher_WithWrongKey_FailsOrProducesGarbage()
    {
        var sdk = new RustSdkService();
        var encryptionKey = sdk.GenerateOrganizationKeys();
        var differentKey = sdk.GenerateOrganizationKeys();

        var originalCipher = CreateTestLoginCipher();
        var cipherJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);

        var encryptedJson = sdk.EncryptCipher(cipherJson, encryptionKey.Key);
        Assert.DoesNotContain("\"error\"", encryptedJson);

        var decryptedJson = sdk.DecryptCipher(encryptedJson, differentKey.Key);

        var decryptionFailedWithError = decryptedJson.Contains("\"error\"");
        if (!decryptionFailedWithError)
        {
            var decrypted = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);
            Assert.NotEqual(originalCipher.Name, decrypted?.Name);
        }
    }

    [Fact]
    public void EncryptCipher_WithFields_EncryptsCustomFields()
    {
        var sdk = new RustSdkService();
        var orgKeys = sdk.GenerateOrganizationKeys();

        var cipher = new CipherViewDto
        {
            Name = "Service Account",
            Type = CipherTypes.Login,
            Login = new LoginViewDto
            {
                Username = "service-account",
                Password = "svc-password"
            },
            Fields =
            [
                new FieldViewDto { Name = "API Key", Value = "sk-secret-api-key-12345", Type = 1 },
                new FieldViewDto { Name = "Client ID", Value = "client-id-xyz", Type = 0 }
            ]
        };

        var cipherJson = JsonSerializer.Serialize(cipher, SdkJsonOptions);
        var encryptedJson = sdk.EncryptCipher(cipherJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("sk-secret-api-key-12345", encryptedJson);
        Assert.DoesNotContain("client-id-xyz", encryptedJson);

        var decryptedJson = sdk.DecryptCipher(encryptedJson, orgKeys.Key);
        var decrypted = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);

        Assert.NotNull(decrypted?.Fields);
        Assert.Equal(2, decrypted.Fields.Count);
        Assert.Equal("API Key", decrypted.Fields[0].Name);
        Assert.Equal("sk-secret-api-key-12345", decrypted.Fields[0].Value);
    }

    [Fact]
    public void CipherSeeder_ProducesServerCompatibleFormat()
    {
        var sdk = new RustSdkService();
        var orgKeys = sdk.GenerateOrganizationKeys();
        var seeder = new CipherSeeder(sdk);
        var orgId = Guid.NewGuid();

        // Create cipher using the seeder
        var cipher = seeder.CreateOrganizationLoginCipher(
            orgId,
            orgKeys.Key,
            name: "GitHub Account",
            username: "developer@example.com",
            password: "SecureP@ss123!",
            uri: "https://github.com",
            notes: "My development account");

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Null(cipher.UserId);
        Assert.Equal(Core.Vault.Enums.CipherType.Login, cipher.Type);
        Assert.NotNull(cipher.Data);

        var loginData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
        Assert.NotNull(loginData);

        var encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, loginData.Name);
        Assert.StartsWith(encStringPrefix, loginData.Username);
        Assert.StartsWith(encStringPrefix, loginData.Password);
        Assert.StartsWith(encStringPrefix, loginData.Notes);

        Assert.NotNull(loginData.Uris);
        var uriData = loginData.Uris.First();
        Assert.StartsWith(encStringPrefix, uriData.Uri);

        Assert.DoesNotContain("GitHub Account", cipher.Data);
        Assert.DoesNotContain("developer@example.com", cipher.Data);
        Assert.DoesNotContain("SecureP@ss123!", cipher.Data);
    }

    [Fact]
    public void CipherSeeder_WithFields_ProducesCorrectServerFormat()
    {
        var sdk = new RustSdkService();
        var orgKeys = sdk.GenerateOrganizationKeys();
        var seeder = new CipherSeeder(sdk);

        var cipher = seeder.CreateOrganizationLoginCipherWithFields(
            Guid.NewGuid(),
            orgKeys.Key,
            name: "API Service",
            username: "service@example.com",
            password: "SvcP@ss!",
            uri: "https://api.example.com",
            fields: [
                ("API Key", "sk-live-abc123", 1),  // Hidden field
                ("Environment", "production", 0)   // Text field
            ]);

        var loginData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
        Assert.NotNull(loginData);
        Assert.NotNull(loginData.Fields);

        var fields = loginData.Fields.ToList();
        Assert.Equal(2, fields.Count);

        var encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, fields[0].Name);
        Assert.StartsWith(encStringPrefix, fields[0].Value);
        Assert.StartsWith(encStringPrefix, fields[1].Name);
        Assert.StartsWith(encStringPrefix, fields[1].Value);

        Assert.Equal(Core.Vault.Enums.FieldType.Hidden, fields[0].Type);
        Assert.Equal(Core.Vault.Enums.FieldType.Text, fields[1].Type);

        Assert.DoesNotContain("API Key", cipher.Data);
        Assert.DoesNotContain("sk-live-abc123", cipher.Data);
    }

    private static CipherViewDto CreateTestLoginCipher()
    {
        return new CipherViewDto
        {
            Name = "Test Login",
            Notes = "Secret notes about this login",
            Type = CipherTypes.Login,
            Login = new LoginViewDto
            {
                Username = "testuser@example.com",
                Password = "SuperSecretP@ssw0rd!",
                Uris = [new LoginUriViewDto { Uri = "https://example.com" }]
            }
        };
    }

}
