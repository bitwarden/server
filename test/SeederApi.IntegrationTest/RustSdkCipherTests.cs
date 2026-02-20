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
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var originalCipher = CreateTestLoginCipher();
        var originalJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);

        var encryptedJson = RustSdkService.EncryptCipher(originalJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.Contains("\"name\":\"2.", encryptedJson);

        var decryptedJson = RustSdkService.DecryptCipher(encryptedJson, orgKeys.Key);

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
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

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
        var encryptedJson = RustSdkService.EncryptCipher(cipherJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("Amazon Shopping", encryptedJson);
        Assert.DoesNotContain("shopper@example.com", encryptedJson);
        Assert.DoesNotContain("MySecretPassword123!", encryptedJson);
    }

    [Fact]
    public void DecryptCipher_WithWrongKey_FailsOrProducesGarbage()
    {
        var encryptionKey = RustSdkService.GenerateOrganizationKeys();
        var differentKey = RustSdkService.GenerateOrganizationKeys();

        var originalCipher = CreateTestLoginCipher();
        var cipherJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);

        var encryptedJson = RustSdkService.EncryptCipher(cipherJson, encryptionKey.Key);
        Assert.DoesNotContain("\"error\"", encryptedJson);

        var decryptedJson = RustSdkService.DecryptCipher(encryptedJson, differentKey.Key);

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
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

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
                new FieldViewDto { Name = "API Key", Value = "sk_test_FAKE_api_key_12345", Type = 1 },
                new FieldViewDto { Name = "Client ID", Value = "client-id-xyz", Type = 0 }
            ]
        };

        var cipherJson = JsonSerializer.Serialize(cipher, SdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptCipher(cipherJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("sk-secret-api-key-12345", encryptedJson);
        Assert.DoesNotContain("client-id-xyz", encryptedJson);

        var decryptedJson = RustSdkService.DecryptCipher(encryptedJson, orgKeys.Key);
        var decrypted = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);

        Assert.NotNull(decrypted?.Fields);
        Assert.Equal(2, decrypted.Fields.Count);
        Assert.Equal("API Key", decrypted.Fields[0].Name);
        Assert.Equal("sk_test_FAKE_api_key_12345", decrypted.Fields[0].Value);
    }

    [Fact]
    public void CipherSeeder_ProducesServerCompatibleFormat()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var orgId = Guid.NewGuid();

        // Create cipher using the seeder
        var cipher = LoginCipherSeeder.Create(
            orgKeys.Key,
            name: "GitHub Account",
            organizationId: orgId,
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
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var cipher = LoginCipherSeeder.Create(
            orgKeys.Key,
            name: "API Service",
            organizationId: Guid.NewGuid(),
            username: "service@example.com",
            password: "SvcP@ss!",
            uri: "https://api.example.com",
            fields: [
                ("API Key", "sk_test_FAKE_abc123", 1),
                ("Environment", "production", 0)
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
        Assert.DoesNotContain("sk_test_FAKE_abc123", cipher.Data);
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

    [Fact]
    public void EncryptDecrypt_CardCipher_RoundtripPreservesPlaintext()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var originalCipher = new CipherViewDto
        {
            Name = "My Visa Card",
            Notes = "Primary card for online purchases",
            Type = CipherTypes.Card,
            Card = new CardViewDto
            {
                CardholderName = "John Doe",
                Brand = "Visa",
                Number = "4111111111111111",
                ExpMonth = "12",
                ExpYear = "2028",
                Code = "123"
            }
        };

        var originalJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptCipher(originalJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("4111111111111111", encryptedJson);
        Assert.DoesNotContain("John Doe", encryptedJson);

        var decryptedJson = RustSdkService.DecryptCipher(encryptedJson, orgKeys.Key);
        var decrypted = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);

        Assert.NotNull(decrypted?.Card);
        Assert.Equal("4111111111111111", decrypted.Card.Number);
        Assert.Equal("John Doe", decrypted.Card.CardholderName);
        Assert.Equal("123", decrypted.Card.Code);
    }

    [Fact]
    public void CipherSeeder_CardCipher_ProducesServerCompatibleFormat()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var orgId = Guid.NewGuid();

        var card = new CardViewDto
        {
            CardholderName = "Jane Smith",
            Brand = "Mastercard",
            Number = "5500000000000004",
            ExpMonth = "06",
            ExpYear = "2027",
            Code = "456"
        };

        var cipher = CardCipherSeeder.Create(orgKeys.Key, name: "Business Card", card: card, organizationId: orgId, notes: "Company expenses");

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Equal(Core.Vault.Enums.CipherType.Card, cipher.Type);

        var cardData = JsonSerializer.Deserialize<CipherCardData>(cipher.Data);
        Assert.NotNull(cardData);

        var encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, cardData.Name);
        Assert.StartsWith(encStringPrefix, cardData.CardholderName);
        Assert.StartsWith(encStringPrefix, cardData.Number);
        Assert.StartsWith(encStringPrefix, cardData.Code);

        Assert.DoesNotContain("5500000000000004", cipher.Data);
        Assert.DoesNotContain("Jane Smith", cipher.Data);
    }

    [Fact]
    public void EncryptDecrypt_IdentityCipher_RoundtripPreservesPlaintext()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var originalCipher = new CipherViewDto
        {
            Name = "Personal Identity",
            Type = CipherTypes.Identity,
            Identity = new IdentityViewDto
            {
                Title = "Mr",
                FirstName = "John",
                MiddleName = "Robert",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Phone = "+1-555-123-4567",
                SSN = "123-45-6789",
                Address1 = "123 Main Street",
                City = "Anytown",
                State = "CA",
                PostalCode = "90210",
                Country = "US"
            }
        };

        var originalJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptCipher(originalJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("123-45-6789", encryptedJson);
        Assert.DoesNotContain("john.doe@example.com", encryptedJson);

        var decryptedJson = RustSdkService.DecryptCipher(encryptedJson, orgKeys.Key);
        var decrypted = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);

        Assert.NotNull(decrypted?.Identity);
        Assert.Equal("John", decrypted.Identity.FirstName);
        Assert.Equal("123-45-6789", decrypted.Identity.SSN);
        Assert.Equal("john.doe@example.com", decrypted.Identity.Email);
    }

    [Fact]
    public void CipherSeeder_IdentityCipher_ProducesServerCompatibleFormat()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var orgId = Guid.NewGuid();

        var identity = new IdentityViewDto
        {
            Title = "Dr",
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice@company.com",
            SSN = "987-65-4321",
            PassportNumber = "X12345678"
        };

        var cipher = IdentityCipherSeeder.Create(orgKeys.Key, name: "Dr. Alice Johnson", identity: identity, organizationId: orgId);

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Equal(Core.Vault.Enums.CipherType.Identity, cipher.Type);

        var identityData = JsonSerializer.Deserialize<CipherIdentityData>(cipher.Data);
        Assert.NotNull(identityData);

        var encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, identityData.Name);
        Assert.StartsWith(encStringPrefix, identityData.FirstName);
        Assert.StartsWith(encStringPrefix, identityData.SSN);

        Assert.DoesNotContain("987-65-4321", cipher.Data);
        Assert.DoesNotContain("Alice", cipher.Data);
    }

    [Fact]
    public void EncryptDecrypt_SecureNoteCipher_RoundtripPreservesPlaintext()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var originalCipher = new CipherViewDto
        {
            Name = "API Secrets",
            Notes = "sk_test_FAKE_abc123xyz789key",
            Type = CipherTypes.SecureNote,
            SecureNote = new SecureNoteViewDto { Type = 0 }
        };

        var originalJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptCipher(originalJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("sk_test_FAKE_abc123xyz789key", encryptedJson);

        var decryptedJson = RustSdkService.DecryptCipher(encryptedJson, orgKeys.Key);
        var decrypted = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);

        Assert.NotNull(decrypted);
        Assert.Equal("API Secrets", decrypted.Name);
        Assert.Equal("sk_test_FAKE_abc123xyz789key", decrypted.Notes);
    }

    [Fact]
    public void CipherSeeder_SecureNoteCipher_ProducesServerCompatibleFormat()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var orgId = Guid.NewGuid();

        var cipher = SecureNoteCipherSeeder.Create(
            orgKeys.Key,
            name: "Production Secrets",
            organizationId: orgId,
            notes: "DATABASE_URL=postgres://user:FAKE_secret@db.example.com/prod");

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Equal(Core.Vault.Enums.CipherType.SecureNote, cipher.Type);

        var noteData = JsonSerializer.Deserialize<CipherSecureNoteData>(cipher.Data);
        Assert.NotNull(noteData);
        Assert.Equal(Core.Vault.Enums.SecureNoteType.Generic, noteData.Type);

        var encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, noteData.Name);
        Assert.StartsWith(encStringPrefix, noteData.Notes);

        Assert.DoesNotContain("postgres://", cipher.Data);
        Assert.DoesNotContain("secret", cipher.Data);
    }

    [Fact]
    public void EncryptDecrypt_SshKeyCipher_RoundtripPreservesPlaintext()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var originalCipher = new CipherViewDto
        {
            Name = "Dev Server Key",
            Type = CipherTypes.SshKey,
            SshKey = new SshKeyViewDto
            {
                PrivateKey = "-----BEGIN FAKE RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA...\n-----END FAKE RSA PRIVATE KEY-----",
                PublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQ... user@host",
                Fingerprint = "SHA256:nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8"
            }
        };

        var originalJson = JsonSerializer.Serialize(originalCipher, SdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptCipher(originalJson, orgKeys.Key);

        Assert.DoesNotContain("\"error\"", encryptedJson);
        Assert.DoesNotContain("BEGIN FAKE RSA PRIVATE KEY", encryptedJson);
        Assert.DoesNotContain("ssh-rsa AAAAB3", encryptedJson);

        var decryptedJson = RustSdkService.DecryptCipher(encryptedJson, orgKeys.Key);
        var decrypted = JsonSerializer.Deserialize<CipherViewDto>(decryptedJson, SdkJsonOptions);

        Assert.NotNull(decrypted?.SshKey);
        Assert.Contains("BEGIN FAKE RSA PRIVATE KEY", decrypted.SshKey.PrivateKey);
        Assert.StartsWith("ssh-rsa", decrypted.SshKey.PublicKey);
        Assert.StartsWith("SHA256:", decrypted.SshKey.Fingerprint);
    }

    [Fact]
    public void CipherSeeder_SshKeyCipher_ProducesServerCompatibleFormat()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var orgId = Guid.NewGuid();

        var sshKey = new SshKeyViewDto
        {
            PrivateKey = "-----BEGIN FAKE OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAA...\n-----END FAKE OPENSSH PRIVATE KEY-----",
            PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIExample test@machine",
            Fingerprint = "SHA256:examplefingerprint123"
        };

        var cipher = SshKeyCipherSeeder.Create(orgKeys.Key, name: "Production Deploy Key", sshKey: sshKey, organizationId: orgId);

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Equal(Core.Vault.Enums.CipherType.SSHKey, cipher.Type);

        var sshData = JsonSerializer.Deserialize<CipherSSHKeyData>(cipher.Data);
        Assert.NotNull(sshData);

        var encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, sshData.Name);
        Assert.StartsWith(encStringPrefix, sshData.PrivateKey);
        Assert.StartsWith(encStringPrefix, sshData.PublicKey);
        Assert.StartsWith(encStringPrefix, sshData.KeyFingerprint);

        Assert.DoesNotContain("BEGIN FAKE OPENSSH PRIVATE KEY", cipher.Data);
        Assert.DoesNotContain("ssh-ed25519", cipher.Data);
    }

}
