using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Attributes;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public sealed class RustSdkCipherTests
{
    private static readonly JsonSerializerOptions _sdkJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void EncryptString_DecryptString_Roundtrip()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var encrypted = RustSdkService.EncryptString("SuperSecretP@ssw0rd!", orgKeys.Key);

        Assert.StartsWith("2.", encrypted);
        Assert.Equal("SuperSecretP@ssw0rd!", RustSdkService.DecryptString(encrypted, orgKeys.Key));
    }

    [Fact]
    public void EncryptFields_DecryptString_Roundtrip()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var cipher = new CipherViewDto
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

        var json = JsonSerializer.Serialize(cipher, _sdkJsonOptions);
        var fieldPathsJson = JsonSerializer.Serialize(EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>());
        var encryptedJson = RustSdkService.EncryptFields(json, fieldPathsJson, orgKeys.Key);

        using var doc = JsonDocument.Parse(encryptedJson);
        var root = doc.RootElement;

        var encryptedName = root.GetProperty("name").GetString()!;
        Assert.StartsWith("2.", encryptedName);

        var decryptedName = RustSdkService.DecryptString(encryptedName, orgKeys.Key);
        Assert.Equal("Test Login", decryptedName);

        var encryptedUsername = root.GetProperty("login").GetProperty("username").GetString()!;
        var decryptedUsername = RustSdkService.DecryptString(encryptedUsername, orgKeys.Key);
        Assert.Equal("testuser@example.com", decryptedUsername);

        var encryptedUri = root.GetProperty("login").GetProperty("uris")[0].GetProperty("uri").GetString()!;
        var decryptedUri = RustSdkService.DecryptString(encryptedUri, orgKeys.Key);
        Assert.Equal("https://example.com", decryptedUri);
    }

    [Fact]
    public void EncryptFields_NoPlaintextLeakage()
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

        var json = JsonSerializer.Serialize(cipher, _sdkJsonOptions);
        var fieldPathsJson = JsonSerializer.Serialize(EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>());
        var encryptedJson = RustSdkService.EncryptFields(json, fieldPathsJson, orgKeys.Key);

        Assert.DoesNotContain("Amazon Shopping", encryptedJson);
        Assert.DoesNotContain("shopper@example.com", encryptedJson);
        Assert.DoesNotContain("MySecretPassword123!", encryptedJson);
        Assert.DoesNotContain("Prime member since 2020", encryptedJson);
        Assert.Contains("\"name\":\"2.", encryptedJson);
    }

    [Fact]
    public void DecryptString_WithWrongKey_Throws()
    {
        var encryptionKey = RustSdkService.GenerateOrganizationKeys();
        var differentKey = RustSdkService.GenerateOrganizationKeys();

        var encrypted = RustSdkService.EncryptString("secret value", encryptionKey.Key);

        Assert.Throws<RustSdkException>(() =>
            RustSdkService.DecryptString(encrypted, differentKey.Key));
    }

    [Fact]
    public void EncryptFields_WithCustomFields_EncryptsFieldNameAndValue()
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

        var json = JsonSerializer.Serialize(cipher, _sdkJsonOptions);
        var fieldPathsJson = JsonSerializer.Serialize(EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>());
        var encryptedJson = RustSdkService.EncryptFields(json, fieldPathsJson, orgKeys.Key);

        Assert.DoesNotContain("sk_test_FAKE_api_key_12345", encryptedJson);
        Assert.DoesNotContain("client-id-xyz", encryptedJson);

        using var doc = JsonDocument.Parse(encryptedJson);
        var fields = doc.RootElement.GetProperty("fields");

        var field0Name = fields[0].GetProperty("name").GetString()!;
        var field0Value = fields[0].GetProperty("value").GetString()!;
        Assert.StartsWith("2.", field0Name);
        Assert.StartsWith("2.", field0Value);

        Assert.Equal("API Key", RustSdkService.DecryptString(field0Name, orgKeys.Key));
        Assert.Equal("sk_test_FAKE_api_key_12345", RustSdkService.DecryptString(field0Value, orgKeys.Key));
    }

    [Fact]
    public void CipherSeeder_ProducesServerCompatibleFormat()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var orgId = Guid.NewGuid();

        var cipher = LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = "GitHub Account",
            EncryptionKey = orgKeys.Key,
            OrganizationId = orgId,
            Notes = "My development account",
            Login = new LoginViewDto
            {
                Username = "developer@example.com",
                Password = "SecureP@ss123!",
                Uris = [new LoginUriViewDto { Uri = "https://github.com" }]
            }
        });

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Null(cipher.UserId);
        Assert.Equal(Core.Vault.Enums.CipherType.Login, cipher.Type);
        Assert.NotNull(cipher.Data);

        var loginData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
        Assert.NotNull(loginData);

        const string encStringPrefix = "2.";
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

        var cipher = LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = "API Service",
            EncryptionKey = orgKeys.Key,
            OrganizationId = Guid.NewGuid(),
            Login = new LoginViewDto
            {
                Username = "service@example.com",
                Password = "SvcP@ss!",
                Uris = [new LoginUriViewDto { Uri = "https://api.example.com" }]
            },
            Fields =
            [
                new FieldViewDto { Name = "API Key", Value = "sk_test_FAKE_abc123", Type = 1 },
                new FieldViewDto { Name = "Environment", Value = "production", Type = 0 }
            ]
        });

        var loginData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
        Assert.NotNull(loginData);
        Assert.NotNull(loginData.Fields);

        var fields = loginData.Fields.ToList();
        Assert.Equal(2, fields.Count);

        const string encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, fields[0].Name);
        Assert.StartsWith(encStringPrefix, fields[0].Value);
        Assert.StartsWith(encStringPrefix, fields[1].Name);
        Assert.StartsWith(encStringPrefix, fields[1].Value);

        Assert.Equal(Core.Vault.Enums.FieldType.Hidden, fields[0].Type);
        Assert.Equal(Core.Vault.Enums.FieldType.Text, fields[1].Type);

        Assert.DoesNotContain("API Key", cipher.Data);
        Assert.DoesNotContain("sk_test_FAKE_abc123", cipher.Data);
    }

    [Fact]
    public void EncryptFields_CardCipher_RoundtripDecrypt()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var cipher = new CipherViewDto
        {
            Name = "My Visa Card",
            Type = CipherTypes.Card,
            Card = new CardViewDto
            {
                CardholderName = "John Doe",
                Number = "4111111111111111",
                ExpMonth = "12",
                ExpYear = "2028",
                Code = "123"
            }
        };

        var json = JsonSerializer.Serialize(cipher, _sdkJsonOptions);
        var fieldPathsJson = JsonSerializer.Serialize(EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>());
        var encryptedJson = RustSdkService.EncryptFields(json, fieldPathsJson, orgKeys.Key);

        using var doc = JsonDocument.Parse(encryptedJson);
        var card = doc.RootElement.GetProperty("card");

        Assert.Equal("John Doe", RustSdkService.DecryptString(card.GetProperty("cardholderName").GetString()!, orgKeys.Key));
        Assert.Equal("4111111111111111", RustSdkService.DecryptString(card.GetProperty("number").GetString()!, orgKeys.Key));
        Assert.Equal("12", RustSdkService.DecryptString(card.GetProperty("expMonth").GetString()!, orgKeys.Key));
        Assert.Equal("2028", RustSdkService.DecryptString(card.GetProperty("expYear").GetString()!, orgKeys.Key));
        Assert.Equal("123", RustSdkService.DecryptString(card.GetProperty("code").GetString()!, orgKeys.Key));
    }

    [Fact]
    public void EncryptFields_IdentityCipher_RoundtripDecrypt()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var cipher = new CipherViewDto
        {
            Name = "Personal Identity",
            Type = CipherTypes.Identity,
            Identity = new IdentityViewDto
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                SSN = "123-45-6789",
                Address1 = "123 Main Street",
                City = "Anytown",
                State = "CA",
                PostalCode = "90210",
                Country = "US"
            }
        };

        var json = JsonSerializer.Serialize(cipher, _sdkJsonOptions);
        var fieldPathsJson = JsonSerializer.Serialize(EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>());
        var encryptedJson = RustSdkService.EncryptFields(json, fieldPathsJson, orgKeys.Key);

        using var doc = JsonDocument.Parse(encryptedJson);
        var identity = doc.RootElement.GetProperty("identity");

        Assert.Equal("John", RustSdkService.DecryptString(identity.GetProperty("firstName").GetString()!, orgKeys.Key));
        Assert.Equal("123-45-6789", RustSdkService.DecryptString(identity.GetProperty("ssn").GetString()!, orgKeys.Key));
        Assert.Equal("john.doe@example.com", RustSdkService.DecryptString(identity.GetProperty("email").GetString()!, orgKeys.Key));
        Assert.Equal("123 Main Street", RustSdkService.DecryptString(identity.GetProperty("address1").GetString()!, orgKeys.Key));
        Assert.Equal("90210", RustSdkService.DecryptString(identity.GetProperty("postalCode").GetString()!, orgKeys.Key));
    }

    [Fact]
    public void EncryptFields_SshKeyCipher_RoundtripDecrypt()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var cipher = new CipherViewDto
        {
            Name = "Dev Key",
            Type = CipherTypes.SshKey,
            SshKey = new SshKeyViewDto
            {
                PrivateKey = "-----BEGIN FAKE KEY-----\nMIIE...\n-----END FAKE KEY-----",
                PublicKey = "ssh-rsa AAAAB3... user@host",
                Fingerprint = "SHA256:abc123"
            }
        };

        var json = JsonSerializer.Serialize(cipher, _sdkJsonOptions);
        var fieldPathsJson = JsonSerializer.Serialize(EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>());
        var encryptedJson = RustSdkService.EncryptFields(json, fieldPathsJson, orgKeys.Key);

        using var doc = JsonDocument.Parse(encryptedJson);
        var sshKey = doc.RootElement.GetProperty("sshKey");

        Assert.Equal("-----BEGIN FAKE KEY-----\nMIIE...\n-----END FAKE KEY-----",
            RustSdkService.DecryptString(sshKey.GetProperty("privateKey").GetString()!, orgKeys.Key));
        Assert.Equal("ssh-rsa AAAAB3... user@host",
            RustSdkService.DecryptString(sshKey.GetProperty("publicKey").GetString()!, orgKeys.Key));
        Assert.Equal("SHA256:abc123",
            RustSdkService.DecryptString(sshKey.GetProperty("fingerprint").GetString()!, orgKeys.Key));
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

        var cipher = CardCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Card,
            Name = "Business Card",
            Notes = "Company expenses",
            EncryptionKey = orgKeys.Key,
            OrganizationId = orgId,
            Card = card
        });

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

        var cipher = IdentityCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Identity,
            Name = "Dr. Alice Johnson",
            EncryptionKey = orgKeys.Key,
            OrganizationId = orgId,
            Identity = identity
        });

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
    public void CipherSeeder_SecureNoteCipher_ProducesServerCompatibleFormat()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var orgId = Guid.NewGuid();

        var cipher = SecureNoteCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.SecureNote,
            Name = "Production Secrets",
            Notes = "DATABASE_URL=postgres://user:FAKE_secret@db.example.com/prod",
            EncryptionKey = orgKeys.Key,
            OrganizationId = orgId
        });

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Equal(Core.Vault.Enums.CipherType.SecureNote, cipher.Type);

        var noteData = JsonSerializer.Deserialize<CipherSecureNoteData>(cipher.Data);
        Assert.NotNull(noteData);
        Assert.Equal(Core.Vault.Enums.SecureNoteType.Generic, noteData.Type);

        Assert.StartsWith("2.", noteData.Name);
        Assert.StartsWith("2.", noteData.Notes);

        Assert.DoesNotContain("postgres://", cipher.Data);
        Assert.DoesNotContain("secret", cipher.Data);
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

        var cipher = SshKeyCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.SSHKey,
            Name = "Production Deploy Key",
            EncryptionKey = orgKeys.Key,
            OrganizationId = orgId,
            SshKey = sshKey
        });

        Assert.Equal(orgId, cipher.OrganizationId);
        Assert.Equal(Core.Vault.Enums.CipherType.SSHKey, cipher.Type);

        var sshData = JsonSerializer.Deserialize<CipherSSHKeyData>(cipher.Data);
        Assert.NotNull(sshData);

        const string encStringPrefix = "2.";
        Assert.StartsWith(encStringPrefix, sshData.Name);
        Assert.StartsWith(encStringPrefix, sshData.PrivateKey);
        Assert.StartsWith(encStringPrefix, sshData.PublicKey);
        Assert.StartsWith(encStringPrefix, sshData.KeyFingerprint);

        Assert.DoesNotContain("BEGIN FAKE OPENSSH PRIVATE KEY", cipher.Data);
        Assert.DoesNotContain("ssh-ed25519", cipher.Data);
    }
}
