#nullable disable
// FIXME: Update this file to be null safe and then delete the line above

using System.Text.Json;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Factories;

/// <summary>
/// Factory for creating vault items (ciphers) with proper encryption
/// </summary>
public class CipherSeeder
{
    /// <summary>
    /// Creates a login item (password)
    /// </summary>
    public static Cipher CreateLogin(
        string name,
        string username,
        string password,
        string uri,
        Guid? userId,
        Guid? organizationId,
        byte[] encryptionKey,
        ISeederCryptoService cryptoService,
        string notes = null,
        string totp = null)
    {
        var loginData = new CipherLoginData
        {
            Name = cryptoService.EncryptText(name, encryptionKey),
            Username = !string.IsNullOrEmpty(username) ? cryptoService.EncryptText(username, encryptionKey) : null,
            Password = !string.IsNullOrEmpty(password) ? cryptoService.EncryptText(password, encryptionKey) : null,
            Notes = !string.IsNullOrEmpty(notes) ? cryptoService.EncryptText(notes, encryptionKey) : null,
            Totp = !string.IsNullOrEmpty(totp) ? cryptoService.EncryptText(totp, encryptionKey) : null,
            Uris = string.IsNullOrEmpty(uri) ? null : new[]
            {
                new CipherLoginData.CipherLoginUriData
                {
                    Uri = cryptoService.EncryptText(uri, encryptionKey),
                    Match = null
                }
            }
        };

        var cipher = new Cipher
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = Core.Vault.Enums.CipherType.Login,
            Data = JsonSerializer.Serialize(loginData),
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };

        return cipher;
    }

    /// <summary>
    /// Creates a secure note
    /// </summary>
    public static Cipher CreateSecureNote(
        string name,
        string notes,
        Guid? userId,
        Guid? organizationId,
        byte[] encryptionKey,
        ISeederCryptoService cryptoService)
    {
        var noteData = new CipherSecureNoteData
        {
            Name = cryptoService.EncryptText(name, encryptionKey),
            Notes = cryptoService.EncryptText(notes, encryptionKey),
            Type = SecureNoteType.Generic
        };

        var cipher = new Cipher
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = Core.Vault.Enums.CipherType.SecureNote,
            Data = JsonSerializer.Serialize(noteData),
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };

        return cipher;
    }

    /// <summary>
    /// Creates a credit card item
    /// </summary>
    public static Cipher CreateCard(
        string name,
        string cardholderName,
        string number,
        string brand,
        string expMonth,
        string expYear,
        string code,
        Guid? userId,
        Guid? organizationId,
        byte[] encryptionKey,
        ISeederCryptoService cryptoService,
        string notes = null)
    {
        var cardData = new CipherCardData
        {
            Name = cryptoService.EncryptText(name, encryptionKey),
            Notes = !string.IsNullOrEmpty(notes) ? cryptoService.EncryptText(notes, encryptionKey) : null,
            CardholderName = !string.IsNullOrEmpty(cardholderName) ? cryptoService.EncryptText(cardholderName, encryptionKey) : null,
            Number = !string.IsNullOrEmpty(number) ? cryptoService.EncryptText(number, encryptionKey) : null,
            Brand = !string.IsNullOrEmpty(brand) ? cryptoService.EncryptText(brand, encryptionKey) : null,
            ExpMonth = !string.IsNullOrEmpty(expMonth) ? cryptoService.EncryptText(expMonth, encryptionKey) : null,
            ExpYear = !string.IsNullOrEmpty(expYear) ? cryptoService.EncryptText(expYear, encryptionKey) : null,
            Code = !string.IsNullOrEmpty(code) ? cryptoService.EncryptText(code, encryptionKey) : null
        };

        var cipher = new Cipher
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = Core.Vault.Enums.CipherType.Card,
            Data = JsonSerializer.Serialize(cardData),
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };

        return cipher;
    }

    /// <summary>
    /// Creates an identity item
    /// </summary>
    public static Cipher CreateIdentity(
        string name,
        string title,
        string firstName,
        string lastName,
        string email,
        string phone,
        Guid? userId,
        Guid? organizationId,
        byte[] encryptionKey,
        ISeederCryptoService cryptoService,
        string notes = null)
    {
        var identityData = new CipherIdentityData
        {
            Name = cryptoService.EncryptText(name, encryptionKey),
            Notes = !string.IsNullOrEmpty(notes) ? cryptoService.EncryptText(notes, encryptionKey) : null,
            Title = !string.IsNullOrEmpty(title) ? cryptoService.EncryptText(title, encryptionKey) : null,
            FirstName = !string.IsNullOrEmpty(firstName) ? cryptoService.EncryptText(firstName, encryptionKey) : null,
            LastName = !string.IsNullOrEmpty(lastName) ? cryptoService.EncryptText(lastName, encryptionKey) : null,
            Email = !string.IsNullOrEmpty(email) ? cryptoService.EncryptText(email, encryptionKey) : null,
            Phone = !string.IsNullOrEmpty(phone) ? cryptoService.EncryptText(phone, encryptionKey) : null
        };

        var cipher = new Cipher
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = Core.Vault.Enums.CipherType.Identity,
            Data = JsonSerializer.Serialize(identityData),
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };

        return cipher;
    }

    /// <summary>
    /// Generates sample login data
    /// </summary>
    public static class SampleData
    {
        public static readonly (string name, string username, string uri, string password)[] Logins = new[]
        {
            ("Google Account", "user@gmail.com", "https://accounts.google.com", "G00gl3P@ssw0rd!"),
            ("GitHub", "developer123", "https://github.com", "GitHubSecure#2024"),
            ("Amazon", "shopper@email.com", "https://amazon.com", "Am@z0nPrime!123"),
            ("Netflix", "binge@watcher.com", "https://netflix.com", "N3tfl!xStream$"),
            ("LinkedIn", "professional@email.com", "https://linkedin.com", "Link3d!nPro2024"),
            ("Twitter/X", "@username", "https://x.com", "Tw33t3r#Secure"),
            ("Facebook", "social@email.com", "https://facebook.com", "F@ceb00k!Meta"),
            ("Microsoft Account", "user@outlook.com", "https://account.microsoft.com", "Micr0s0ft!365"),
            ("Apple ID", "apple@icloud.com", "https://appleid.apple.com", "Appl3!Cloud#ID"),
            ("Dropbox", "storage@user.com", "https://dropbox.com", "Dr0pb0x!Sync")
        };

        public static readonly (string name, string content)[] SecureNotes = new[]
        {
            ("WiFi Password", "Network: HomeWiFi\nPassword: SuperSecure#WiFi2024\nRouter IP: 192.168.1.1"),
            ("Server Credentials", "Host: prod-server-01\nSSH Port: 22\nUsername: admin\nKey Location: ~/.ssh/prod_rsa"),
            ("API Keys", "GitHub Token: ghp_xxxxxxxxxxxxxxxxxxxx\nAWS Access: AKIAIOSFODNN7EXAMPLE"),
            ("Recovery Codes", "Google: 1234 5678\nGitHub: abcd efgh\nMicrosoft: 9876 5432"),
            ("Meeting Notes", "Project standup every Monday 9am\nZoom ID: 123-456-7890\nPasscode: Meet123")
        };

        public static readonly (string name, string number, string holder, string brand)[] Cards = new[]
        {
            ("Personal Visa", "4111111111111111", "John Doe", "Visa"),
            ("Business MasterCard", "5500000000000004", "Jane Smith", "MasterCard"),
            ("Travel Card", "340000000000009", "Travel Account", "American Express"),
            ("Online Shopping", "6011000000000004", "Shop User", "Discover"),
            ("Backup Card", "4222222222222", "Emergency Fund", "Visa")
        };

        public static readonly (string title, string first, string last, string email)[] Identities = new[]
        {
            ("Mr.", "John", "Doe", "john.doe@example.com"),
            ("Ms.", "Jane", "Smith", "jane.smith@example.com"),
            ("Dr.", "Robert", "Johnson", "dr.johnson@medical.com"),
            ("Mrs.", "Mary", "Williams", "mary.williams@example.com"),
            ("Prof.", "David", "Brown", "prof.brown@university.edu")
        };
    }
}
